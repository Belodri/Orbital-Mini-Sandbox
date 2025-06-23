using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TsTypeGen;

internal record ParsedProperty(string Name, string Comment);
internal record ParsedRecord(string Name, List<ParsedProperty> Properties);

internal static class Program
{
    private const string SolutionFileName = "OrbitalMiniSandbox.sln";

    // Configure input & output files
    private static readonly string InputFileRelativePath = "src/Bridge/LayoutRecords.cs";
    private static readonly string[] OutputFileRelativePaths  = [
        "src/Bridge/types/LayoutRecords.d.ts",
    ];

    public static async Task<int> Main()
    {
        Console.WriteLine("Generating TypeScript Types...");

        try
        {
            string repoRoot = FindRepoRoot();
            
            string csharpContent = await ReadSourceFileAsync(repoRoot);
            List<ParsedRecord> parsedRecords = Parser.ParseSource(csharpContent);
            string tsContent = TsGenerator.Generate(parsedRecords);

            if (string.IsNullOrWhiteSpace(tsContent))
            {
                Console.WriteLine("Warning: No records found. Aborting...");
                return 1;
            }

            var tasks = OutputFileRelativePaths.Select(relPath => WriteOutputFileAsync(repoRoot, relPath, tsContent));
            await Task.WhenAll(tasks);

            Console.WriteLine("LayoutRecords.d.ts was generated successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 0;
        }
    }
    
    private static string FindRepoRoot()
    {
        string currentPath = AppContext.BaseDirectory;
        DirectoryInfo? directory = new(currentPath);

        // Loop upwards until we find the .sln file or hit the filesystem root.
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new DirectoryNotFoundException($"Could not find repository root. Searched upwards for '{SolutionFileName}'.");
        }
        
        return directory.FullName;
    }

    private static async Task<string> ReadSourceFileAsync(string repoRoot)
    {
        var inputFile = Path.Combine(repoRoot, InputFileRelativePath);
        Console.WriteLine($"Input C# File:  {inputFile}");

        if (!File.Exists(inputFile))
        {
            throw new FileNotFoundException($"Input file not found at '{inputFile}'");
        }

        return await File.ReadAllTextAsync(inputFile);
    }

    private static async Task WriteOutputFileAsync(string repoRoot, string relPath, string content)
    {

        var outputFile = Path.Combine(repoRoot, relPath);
        Console.WriteLine($"Target Output TS File: {outputFile}");

        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        await File.WriteAllTextAsync(outputFile, content);
    }
}

internal static class Parser
{
    public static List<ParsedRecord> ParseSource(string csharpContent)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(csharpContent);
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

                    // Skip internal properties starting with '_'
                    if (propName.StartsWith('_')) continue;

                    var comment = GetDocumentationComment(parameterNode);
                    properties.Add(new ParsedProperty(propName, comment));
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

        var xmlTrivia = trivia.GetStructure() as DocumentationCommentTriviaSyntax;
        if (xmlTrivia == null) return string.Empty;

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
        string interfaceName = record.Name.Replace("Rec", "");
        builder.AppendLine($"export interface {interfaceName} {{");

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
        
        builder.AppendLine($"    readonly {prop.Name}: number;");
    }
}