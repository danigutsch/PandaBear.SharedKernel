using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PandaBear.Style.Analyzers.Tests;

/// <summary>Reads the compiler inputs that the build records for the governed literal guard.</summary>
internal static class GovernedLiteralManifest
{
    /// <summary>Creates the compilation that a recorded <c>*.inputs</c> manifest describes.</summary>
    public static CSharpCompilation Compile(string manifest)
    {
        var lines = File.ReadAllLines(manifest).Select(line => line.Split('|', 2)).ToArray();
        var assembly = lines.ShouldHaveSingleItem(line => line[0] == "assembly")[1];
        var defines = lines.ShouldHaveSingleItem(line => line[0] == "defines")[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
        var options = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: defines);
        var trees = lines.Where(line => line[0] == "source").Select(line =>
            CSharpSyntaxTree.ParseText(File.ReadAllText(line[1]), options, line[1]));
        var references = lines.Where(line => line[0] == "reference").Select(line => MetadataReference.CreateFromFile(line[1]));
        return CSharpCompilation.Create(assembly, trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
