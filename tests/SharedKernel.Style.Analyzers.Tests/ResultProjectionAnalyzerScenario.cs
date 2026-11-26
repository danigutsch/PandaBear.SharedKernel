using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SharedKernel.Results;

namespace SharedKernel.Style.Analyzers.Tests;

/// <summary>Runs the optional projection rule against source using the actual Results assembly.</summary>
internal static class ResultProjectionAnalyzerScenario
{
    private const string DiagnosticId = "SKSTYLE019";

    /// <summary>Compiles the source and returns diagnostics from the enabled projection rule.</summary>
    internal static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        var compilation = Testing.Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            string.Empty,
            [typeof(Result).Assembly]);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic> { [DiagnosticId] = ReportDiagnostic.Warn }));
        var ct = TestContext.Current.CancellationToken;
        compilation.GetDiagnostics(ct).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();
        var diagnostics = await compilation.WithAnalyzers([new StyleAnalyzer()]).GetAnalyzerDiagnosticsAsync(ct);
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == "AD0001");
        return diagnostics.Where(static diagnostic => diagnostic.Id == DiagnosticId).ToImmutableArray();
    }
}
