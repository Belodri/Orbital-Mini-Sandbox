using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TsTypeGen;

internal record ParsedProperty(string Name, string Comment);
internal record ParsedRecord(string Name, List<ParsedProperty> Properties);

internal static class Program
{
    // Configure input & output files
    private const string InputCsFile = "../../src/Bridge/LayoutRecords.cs";
    private static readonly string[] OutputTsFiles = [
        "../../src/Bridge/types/LayoutRecords.d.ts",
    ];

    public static async Task<int> Main()
    {
        Console.WriteLine("Generating TypeScript Types...");

        try
        {
            string csharpContent = await ReadSourceFileAsync();
            List<ParsedRecord> parsedRecords = Parser.ParseSource(csharpContent);
            string tsContent = TsGenerator.Generate(parsedRecords);

            if (string.IsNullOrWhiteSpace(tsContent))
            {
                Console.WriteLine("Warning: No records found. Aborting...");
                return 1;
            }

            var tasks = OutputTsFiles.Select(path => WriteOutputFileAsync(path, tsContent));
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

    private static async Task<string> ReadSourceFileAsync()
    {
        var inputFile = Path.GetFullPath(InputCsFile);
        Console.WriteLine($"Input C# File:  {inputFile}");
        
        if (!File.Exists(inputFile))
        {
            throw new FileNotFoundException($"Input file not found at '{inputFile}'");
        }
        
        return await File.ReadAllTextAsync(inputFile);
    }

    private static async Task WriteOutputFileAsync(string path, string content)
    {
        var outputFile = Path.GetFullPath(path);
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

        return string.Concat(summaryElement.Content.Select(c => c.ToString())).Trim();
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