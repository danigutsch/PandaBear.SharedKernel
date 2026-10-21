using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace PandaBear.Style.Analyzers.Tests;

internal static class GovernedLiteralTestHarness
{
    public static async Task<ImmutableArray<Diagnostic>> Analyze(string source, string? catalog)
    {
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production")
            .RemoveAllSyntaxTrees().AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, path: "Consumer.cs"));
        var options = new AnalyzerOptions(catalog is null ? [] : [new CatalogText(catalog)]);
        return await compilation.WithAnalyzers([new SharedKernelStyleAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

}
