using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers.Tests;

/// <summary>
/// Builds source and runs the SharedKernel style analyzer to collect SKSTYLE015 diagnostics for case-insensitive JSON context tests.
/// </summary>
internal static class CaseInsensitiveJsonContextTestHarness
{
    private const string DiagnosticId = "SKSTYLE015";

    /// <summary>
    /// Returns source for a Payload record and a partial GitHubJsonContext marked with the given attribute line.
    /// </summary>
    /// <param name="attribute">The attribute text placed on the JSON serializer context.</param>
    public static string CreateSource(string attribute) => $$"""
        using System.Text.Json;
        using System.Text.Json.Serialization;
        public sealed record Payload(string Name);
        {{attribute}}
        [JsonSerializable(typeof(Payload))]
        internal sealed partial class GitHubJsonContext : JsonSerializerContext;
        """;

    /// <summary>
    /// Compiles the source as the "Production" assembly, optionally raises SKSTYLE015 to a warning, and returns only the SKSTYLE015 diagnostics from the style analyzer.
    /// </summary>
    /// <param name="source">The C# source to analyze.</param>
    /// <param name="enabled">Whether SKSTYLE015 is enabled as a warning.</param>
    public static async Task<IReadOnlyList<Diagnostic>> GetDiagnostics(string source, bool enabled)
    {
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production");
        if (enabled)
        {
            compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic> { [DiagnosticId] = ReportDiagnostic.Warn }));
        }

        var diagnostics = await compilation.WithAnalyzers([new SharedKernelStyleAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        return [.. diagnostics.Where(static candidate => candidate.Id == DiagnosticId)];
    }
}
