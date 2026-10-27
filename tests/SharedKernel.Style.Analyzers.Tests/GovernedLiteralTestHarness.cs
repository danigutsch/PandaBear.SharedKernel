using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace PandaBear.Style.Analyzers.Tests;

/// <summary>
/// Runs the SharedKernel style analyzer on a consumer source file with an optional governed literal catalog.
/// </summary>
internal static class GovernedLiteralTestHarness
{
    /// <summary>
    /// Compiles the source as "Consumer.cs" in the "Production" assembly and returns the analyzer diagnostics, supplying the catalog as an additional file when it is not null.
    /// </summary>
    /// <param name="source">The C# source to analyze.</param>
    /// <param name="catalog">The governed literal catalog text, or null for no catalog.</param>
    public static async Task<ImmutableArray<Diagnostic>> Analyze(string source, string? catalog)
    {
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production")
            .RemoveAllSyntaxTrees().AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, path: "Consumer.cs"));
        var options = new AnalyzerOptions(catalog is null ? [] : [new CatalogText(catalog)]);
        return await compilation.WithAnalyzers([new SharedKernelStyleAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

}
