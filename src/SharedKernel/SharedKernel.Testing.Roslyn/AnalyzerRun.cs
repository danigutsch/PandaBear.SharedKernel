using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Testing.Roslyn;

/// <summary>Runs one analyzer over a compilation and returns what it reports.</summary>
public static class AnalyzerRun
{
    /// <summary>Runs <paramref name="analyzer"/> over <paramref name="compilation"/> with the given global analyzer config options.</summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <param name="globalOptions">The global <c>.editorconfig</c> options the analyzer reads, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The diagnostics the analyzer reported, without the compiler's own.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="compilation"/> or <paramref name="analyzer"/> is <see langword="null"/>.</exception>
    public static Task<ImmutableArray<Diagnostic>> GetDiagnostics(
        Compilation compilation,
        DiagnosticAnalyzer analyzer,
        ImmutableDictionary<string, string>? globalOptions = null,
        CancellationToken cancellationToken = default) =>
        GetDiagnostics(compilation, analyzer, new TestAnalyzerConfigOptionsProvider(globalOptions), cancellationToken);

    /// <summary>Runs <paramref name="analyzer"/> over <paramref name="compilation"/> with the given analyzer config options, for example per-file options.</summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <param name="optionsProvider">The analyzer config options the analyzer reads.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The diagnostics the analyzer reported, without the compiler's own.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="compilation"/>, <paramref name="analyzer"/>, or <paramref name="optionsProvider"/> is <see langword="null"/>.</exception>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnostics(
        Compilation compilation,
        DiagnosticAnalyzer analyzer,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(optionsProvider);
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false,
            analyzerExceptionFilter: null,
            _ => optionsProvider);
        return await compilation.WithAnalyzers([analyzer], options)
            .GetAnalyzerDiagnosticsAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
