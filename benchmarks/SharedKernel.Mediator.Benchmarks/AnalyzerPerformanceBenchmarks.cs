using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SharedKernel.Mediator.Analyzers;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures mediator analyzer throughput across source size, diagnostic density, and analyzer options.
/// </summary>
[Config(typeof(BenchmarkOutputConfig))]
[MemoryDiagnoser]
public class AnalyzerPerformanceBenchmarks
{
    /// <summary>
    /// Gets or sets the number of request/handler pairs emitted into the synthetic project.
    /// </summary>
    [Params(10, 100, 1000, 5000)]
    public int RequestCount { get; set; }

    /// <summary>
    /// Gets or sets whether the source is clean or intentionally diagnostic-heavy.
    /// </summary>
    [Params(AnalyzerBenchmarkSourceFactory.NoDiagnostics, AnalyzerBenchmarkSourceFactory.ManyDiagnostics)]
    public string DiagnosticsMode { get; set; } = AnalyzerBenchmarkSourceFactory.NoDiagnostics;

    /// <summary>
    /// Gets or sets whether strict architecture rules are enabled.
    /// </summary>
    [Params(false, true)]
    public bool StrictArchitectureRules { get; set; }

    /// <summary>
    /// Gets or sets whether cancellation analysis is enabled.
    /// </summary>
    [Params(false, true)]
    public bool CancellationScan { get; set; }

    /// <summary>
    /// Runs the analyzer against the current synthetic project and returns the diagnostic count.
    /// </summary>
    /// <returns>The number of diagnostics reported by the analyzer.</returns>
    [Benchmark]
    public async Task<int> AnalyzeProject()
    {
        var source = AnalyzerBenchmarkSourceFactory.CreateSource(RequestCount, DiagnosticsMode);
        var compilation = BenchmarkCompilationFactory.CreateCompilation(
            source,
            $"SharedKernel.Mediator.Benchmarks.Analyzers.{RequestCount}.{DiagnosticsMode}.{StrictArchitectureRules}.{CancellationScan}");
        var analyzer = new SharedKernelMediatorAnalyzer();
        var optionsProvider = new BenchmarkAnalyzerConfigOptionsProvider(
            ImmutableDictionary<string, string>.Empty
                .Add("sharedkernel_mediator_cqrs_strict", StrictArchitectureRules.ToString())
                .Add("sharedkernel_mediator_enable_cancellation_analysis", CancellationScan.ToString()));
        var compilationOptions = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false,
            analyzerExceptionFilter: null,
            _ => optionsProvider);
        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer], compilationOptions);

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        return diagnostics.Length;
    }

    private sealed class BenchmarkAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string>? globalOptions)
        : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions global = new BenchmarkAnalyzerConfigOptions(globalOptions);

        public override AnalyzerConfigOptions GlobalOptions => global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return BenchmarkAnalyzerConfigOptions.Empty;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return BenchmarkAnalyzerConfigOptions.Empty;
        }
    }

    private sealed class BenchmarkAnalyzerConfigOptions(ImmutableDictionary<string, string>? values)
        : AnalyzerConfigOptions
    {
        public static readonly BenchmarkAnalyzerConfigOptions Empty = new(null);

        private readonly ImmutableDictionary<string, string> options = values ?? ImmutableDictionary<string, string>.Empty;

        public override bool TryGetValue(string key, out string value)
        {
            return options.TryGetValue(key, out value!);
        }
    }
}
