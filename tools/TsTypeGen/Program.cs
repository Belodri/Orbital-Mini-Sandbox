using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TsTypeGen;

internal record ParsedProperty(string Name, string Comment, string TypeScriptType);
internal record ParsedRecord(string Name, IReadOnlyList<ParsedProperty> Properties);

internal static class Program
{
    private const string SolutionFileName = "OrbitalMiniSandbox.sln";
    private const string UsageHint = "Usage (all paths relative to OrbitalMiniSandbox.sln): dotnet run --project <project_path> -- --input <path> --output <path_to_.d.ts>";

    public static async Task<int> Main(string[] args)   // Use System.CommandLine if adding any more arguments
    {
        try
        {
            var outPath = await MainWorker(args);
            Console.WriteLine($"Successfully generated \"{outPath}\"!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;   // Return non-zero exit code for failure
        }
    }

    private static async Task<string> MainWorker(string[] args)
    {
        var (inPath, outPath) = ParseArguments(args);
        var repoRoot = FindRepoRoot();

        var cSharpSourceCode = await ReadSourceFileAsync(repoRoot, inPath);
        List<ParsedRecord> parsedRecords = Parser.ParseSource(cSharpSourceCode);
        string tsContent = TsGenerator.Generate(parsedRecords);

        if (string.IsNullOrWhiteSpace(tsContent)) throw new Exception($"No records found in {inPath}.");

        await WriteOutputFileAsync(repoRoot, outPath, tsContent);

        return outPath;
    }

    private static (string inputFileRelativePath, string outputFileRelativePath) ParseArguments(string[] args)
    {
        string? relInPath = null;
        string? relOutPath = null;

        for (int i = 0; i + 1 < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input": relInPath = args[++i]; break;
                case "--output": relOutPath = args[++i]; break;
            }
        }

        if(string.IsNullOrWhiteSpace(relInPath) || string.IsNullOrWhiteSpace(relOutPath)) throw new Exception($"Missing required arguments. \n{UsageHint}");
        return (relInPath, relOutPath);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        // Loop upwards until we find the .sln file or hit the filesystem root.
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName))) directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException($"Could not find repository root. Searched upwards for '{SolutionFileName}'.");
    }

    private static async Task<string> ReadSourceFileAsync(string repoRoot, string relInPath)
    {
        var inputFile = Path.Combine(repoRoot, relInPath);
        return File.Exists(inputFile) ? await File.ReadAllTextAsync(inputFile) : throw new FileNotFoundException($"Input file not found at '{inputFile}'");
    }

    private static async Task WriteOutputFileAsync(string repoRoot, string relOutPath, string content)
    {
        var outputFile = Path.Combine(repoRoot, relOutPath);
        var dirName = Path.GetDirectoryName(outputFile);
        if (string.IsNullOrWhiteSpace(dirName)) throw new Exception($"Output path at '{outputFile}' is root, null, or does not contain directory information.");
        Directory.CreateDirectory(dirName);
        await File.WriteAllTextAsync(outputFile, content);
    }
}

internal static class Parser
{
    private class Validator
    {
        private static readonly (SpecialType cSharpType, string tsType)[] TypeMap = [
            (SpecialType.System_Int32, "number"),
            (SpecialType.System_Double, "number"),
            (SpecialType.System_Boolean, "boolean"),
        ];

        private readonly SemanticModel _model;
        private readonly (ISymbol symbol, string tsType)[] _values;

        public Validator(SyntaxTree tree)
        {
            _model = CSharpCompilation.Create("AssemblyName")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree)
                .GetSemanticModel(tree);

            _values = [.. TypeMap.Select(t => (_model.Compilation.GetSpecialType(t.cSharpType), t.tsType))];
        }

        public string GetTsTypeString(ParameterSyntax param)
        {
            ITypeSymbol? paramTypeSymbol = _model.GetTypeInfo(param.Type!).Type
                ?? throw new InvalidOperationException($"Could not determine the type for parameter '{param.Identifier.ValueText}'.");

            foreach (var (symbol, tsType) in _values) if (SymbolEqualityComparer.Default.Equals(symbol, paramTypeSymbol)) return tsType;
            throw new NotSupportedException($"Type '{paramTypeSymbol.ToDisplayString()}' for property '{param.Identifier.ValueText}' is not supported.");
        }
    }

    public static List<ParsedRecord> ParseSource(string csharpContent)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(csharpContent);
        Validator validator = new(tree);
        var root = tree.GetRoot();

        var allRecords = new List<ParsedRecord>();
        var recordDeclarations = root.DescendantNodes().OfType<RecordDeclarationSyntax>();

        foreach (var recordNode in recordDeclarations)
        {
            var recordName = recordNode.Identifier.ValueText;
            var properties = new List<ParsedProperty>();

            // The properties are defined in the record's "parameter list" (primary constructor).
            if (recordNode.ParameterList is not null)
            {
                foreach (var parameterNode in recordNode.ParameterList.Parameters)
                {
                    var propName = parameterNode.Identifier.ValueText;

                    var comment = GetDocumentationComment(parameterNode);
                    var tsType = validator.GetTsTypeString(parameterNode);

                    properties.Add(new ParsedProperty(propName, comment, tsType));
                }
            }

            allRecords.Add(new ParsedRecord(recordName, properties));
        }

        return allRecords;
    }

    // Extract the XML documentation 'summary' content for a given syntax node.
    private static string GetDocumentationComment(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        if (trivia == default) return string.Empty;

        if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax xmlTrivia) return string.Empty;

        var summaryElement = xmlTrivia.Content
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

        if (summaryElement == null) return string.Empty;

        var textParts = summaryElement.Content
            .SelectMany(n => n.DescendantTokens())
            .Where(t => t.IsKind(SyntaxKind.XmlTextLiteralToken))
            .Select(t => t.ValueText.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));

        return string.Join(" ", textParts);
    }
}

internal static class TsGenerator
{
    public static string Generate(List<ParsedRecord> records)
    {
        if (records.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("// AUTO-GENERATED FILE FROM C# SSOT. DO NOT EDIT.");
        builder.AppendLine("// Re-run the TsTypeGen tool to regenerate.");
        builder.AppendLine();

        foreach (var record in records)
        {
            AppendInterface(builder, record);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendInterface(StringBuilder builder, ParsedRecord record)
    {
        builder.AppendLine($"export interface {record.Name} {{");

        foreach (var prop in record.Properties)
        {
            AppendProperty(builder, prop);
        }

        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendProperty(StringBuilder builder, ParsedProperty prop)
    {
        if (!string.IsNullOrWhiteSpace(prop.Comment))
        {
            builder.AppendLine("    /**");
            builder.AppendLine($"     * {prop.Comment}");
            builder.AppendLine("     */");
        }

        builder.AppendLine($"    {prop.Name}: {prop.TypeScriptType};");
    }
}