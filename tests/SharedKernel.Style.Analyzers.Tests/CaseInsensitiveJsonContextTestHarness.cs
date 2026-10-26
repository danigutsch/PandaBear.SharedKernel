using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers.Tests;

internal static class CaseInsensitiveJsonContextTestHarness
{
    private const string DiagnosticId = "SKSTYLE015";

    public static string CreateSource(string attribute) => $$"""
        using System.Text.Json;
        using System.Text.Json.Serialization;
        public sealed record Payload(string Name);
        {{attribute}}
        [JsonSerializable(typeof(Payload))]
        internal sealed partial class GitHubJsonContext : JsonSerializerContext;
        """;

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
