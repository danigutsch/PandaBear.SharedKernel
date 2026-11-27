using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SharedKernel.Results;

namespace SharedKernel.Style.Analyzers.Tests;

/// <summary>Runs the optional projection rule against source with controlled assembly references.</summary>
internal static class ResultProjectionAnalyzerScenario
{
    private const string DiagnosticId = "SKSTYLE019";

    /// <summary>Compiles the source and returns diagnostics from the enabled projection rule.</summary>
    internal static async Task<ImmutableArray<Diagnostic>> Analyze(
        string source, bool withResults = true, OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        var compilation = Testing.Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            string.Empty,
            [typeof(Result).Assembly]);
        if (!withResults)
        {
            compilation = compilation.RemoveReferences(compilation.References.Where(static reference =>
                string.Equals(Path.GetFileNameWithoutExtension(reference.Display), "SharedKernel.Results", StringComparison.Ordinal)));
            compilation.GetTypeByMetadataName("SharedKernel.Results.Result`1").ShouldBeNull();
        }

        compilation = compilation.WithOptions(compilation.Options.WithOutputKind(outputKind).WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic> { [DiagnosticId] = ReportDiagnostic.Warn }));
        var ct = TestContext.Current.CancellationToken;
        compilation.GetDiagnostics(ct).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();
        var diagnostics = await compilation.WithAnalyzers([new StyleAnalyzer()]).GetAnalyzerDiagnosticsAsync(ct);
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == "AD0001");
        return diagnostics.Where(static diagnostic => diagnostic.Id == DiagnosticId).ToImmutableArray();
    }
}
