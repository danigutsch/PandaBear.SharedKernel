using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Testing.Roslyn;

/// <summary>Reads the compiler inputs that the build records for the governed literal guard.</summary>
public static class GovernedLiteralManifest
{
    /// <summary>Creates the compilation that a recorded <c>*.inputs</c> manifest describes.</summary>
    /// <param name="manifest">The manifest path. All source and reference paths are absolute.</param>
    /// <returns>The compilation for semantic checks on the recorded sources.</returns>
    /// <exception cref="ArgumentException"><paramref name="manifest"/> is empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The manifest lacks exactly one assembly or defines row.</exception>
    /// <exception cref="IOException">An input file cannot be read.</exception>
    public static CSharpCompilation Compile(string manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest);
        var lines = File.ReadAllLines(manifest).Select(line => line.Split('|', 2)).ToArray();
        var assembly = lines.Single(line => line[0] == "assembly")[1];
        var defines = lines.Single(line => line[0] == "defines")[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
        var options = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: defines);
        var trees = lines.Where(line => line[0] == "source").Select(line =>
            CSharpSyntaxTree.ParseText(File.ReadAllText(line[1]), options, line[1]));
        var references = lines.Where(line => line[0] == "reference").Select(line => MetadataReference.CreateFromFile(line[1]));
        return CSharpCompilation.Create(assembly, trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
