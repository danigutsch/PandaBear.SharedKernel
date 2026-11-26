using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>Collects or checks performance baselines for the shipped diagnostic analyzers.</summary>
internal static class AnalyzerRegressionCommand
{
    private const int SchemaVersion = 4;
    private const int Samples = 9;
    private const int Iterations = 5;
    private static readonly int[] SourceCounts = [10, 100, 500];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly CompilationWithAnalyzersOptions AnalysisOptions = new(
        new AnalyzerOptions([]), null, concurrentAnalysis: false, logAnalyzerExecutionTime: true, reportSuppressedDiagnostics: false);

    /// <summary>Collects a new baseline or checks current measurements against an existing one, returning the process exit code.</summary>
    /// <param name="mode">Either <c>collect</c> or <c>check</c>.</param>
    /// <param name="path">Path of the baseline JSON file to write or read.</param>
    public static async Task<int> Run(string mode, string path)
    {
        if (mode is not ("collect" or "check"))
        {
            await Console.Error.WriteLineAsync("Use --analyzer-regression collect|check <baseline.json>.").ConfigureAwait(false);
            return 2;
        }

        var settings = new AnalyzerRegressionBaseline(SchemaVersion, RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            Environment.GetEnvironmentVariable("COMPlus_TieredCompilation"), SourceCounts, Samples, Iterations,
            1.5, 1.2, []);
        AnalyzerRegressionBaseline? baseline = null;
        if (mode == "check")
        {
            baseline = JsonSerializer.Deserialize<AnalyzerRegressionBaseline>(await File.ReadAllTextAsync(path).ConfigureAwait(false))
                ?? throw new InvalidOperationException("Missing analyzer baseline.");
            if (!IsComparable(baseline, settings))
            {
                await Console.Error.WriteLineAsync("Incomparable analyzer baseline. Use the recorded runtime, OS, architecture, tiering and workload settings.")
                    .ConfigureAwait(false);
                return 2;
            }
        }

        var measurements = new List<AnalyzerRegressionMeasurement>();
        foreach (var sourceCount in SourceCounts)
        {
            foreach (var (analyzer, source) in AnalyzerRegressionWorkloads.Create(sourceCount))
            {
                var suite = analyzer.GetType().Name + "/" + sourceCount.ToString(CultureInfo.InvariantCulture);
                var measurement = await Measure(suite, analyzer, source).ConfigureAwait(false);
                measurements.Add(measurement);
                Console.WriteLine(JsonSerializer.Serialize(measurement));
            }
        }

        if (baseline is null)
        {
            var actual = settings with { Measurements = [.. measurements] };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(actual, JsonOptions) + Environment.NewLine).ConfigureAwait(false);
            return 0;
        }

        if (baseline.Measurements.Length != measurements.Count)
        {
            await Console.Error.WriteLineAsync("The analyzer baseline inventory does not match the measured suites.").ConfigureAwait(false);
            return 2;
        }

        var failed = false;
        foreach (var measurement in measurements)
        {
            var expected = baseline.Measurements.SingleOrDefault(item => item.Suite == measurement.Suite);
            if (expected is null || expected.Diagnostics != measurement.Diagnostics || expected.AllocatedBytes <= 0
                || measurement.AllocatedBytes > expected.AllocatedBytes * baseline.MaximumAllocationRatio
                || !WithinOverheadBand(expected.CompilerComparison?.MedianPairedElapsedRatio,
                    measurement.CompilerComparison.MedianPairedElapsedRatio, baseline.MaximumCompilerOverheadRatio))
            {
                await Console.Error.WriteLineAsync("Analyzer regression threshold exceeded or baseline invalid: " + measurement.Suite).ConfigureAwait(false);
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }

    private static bool WithinOverheadBand(double? expected, double? actual, double maximumRatio) =>
        expected is { } reference && double.IsFinite(reference) && reference > 0
        && actual is { } measured && double.IsFinite(measured) && measured > 0
        && measured <= reference * maximumRatio;

    private static bool IsComparable(AnalyzerRegressionBaseline baseline, AnalyzerRegressionBaseline settings) =>
        baseline.SchemaVersion == settings.SchemaVersion
        && baseline.Runtime == settings.Runtime
        && baseline.OperatingSystem == settings.OperatingSystem
        && baseline.Architecture == settings.Architecture
        && baseline.DotnetTieredCompilation == settings.DotnetTieredCompilation
        && baseline.ComPlusTieredCompilation == settings.ComPlusTieredCompilation
        && baseline.SourceCounts is not null
        && baseline.SourceCounts.SequenceEqual(settings.SourceCounts)
        && baseline.Samples == settings.Samples
        && baseline.Iterations == settings.Iterations
        && baseline.MaximumCompilerOverheadRatio == settings.MaximumCompilerOverheadRatio
        && baseline.MaximumAllocationRatio == settings.MaximumAllocationRatio;

    private static async Task<AnalyzerRegressionMeasurement> Measure(string suite, DiagnosticAnalyzer analyzer, string source)
    {
        var compilation = BenchmarkCompilationFactory.CreateCompilation(source);
        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidOperationException(suite + ": " + string.Join(Environment.NewLine, errors.Select(static error => error.ToString())));
        }

        for (var warmup = 0; warmup < 10; warmup++)
        {
            _ = await Analyze(compilation, analyzer).ConfigureAwait(false);
        }

        using var process = Process.GetCurrentProcess();
        var times = new double[Samples];
        var cpuTimes = new double[Samples];
        var analyzerTimes = new double[Samples];
        var allocations = new long[Samples];
        var count = -1;
        for (var sample = 0; sample < Samples; sample++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalAllocatedBytes(precise: true);
            process.Refresh();
            var cpuStart = process.TotalProcessorTime;
            var start = Stopwatch.GetTimestamp();
            var analyzerMilliseconds = 0d;
            for (var iteration = 0; iteration < Iterations; iteration++)
            {
                var result = await Analyze(compilation, analyzer).ConfigureAwait(false);
                var diagnostics = result.GetAllDiagnostics(analyzer);
                if (diagnostics.Any(static diagnostic => diagnostic.Id == "AD0001")
                    || diagnostics.Length == 0 || count >= 0 && count != diagnostics.Length)
                {
                    throw new InvalidOperationException("Analyzer benchmark did not produce stable, nonempty diagnostics: " + suite);
                }

                count = diagnostics.Length;
                analyzerMilliseconds += result.AnalyzerTelemetryInfo[analyzer].ExecutionTime.TotalMilliseconds;
            }

            process.Refresh();
            cpuTimes[sample] = (process.TotalProcessorTime - cpuStart).TotalMilliseconds / Iterations;
            times[sample] = Stopwatch.GetElapsedTime(start).TotalMilliseconds / Iterations;
            analyzerTimes[sample] = analyzerMilliseconds / Iterations;
            allocations[sample] = (GC.GetTotalAllocatedBytes(precise: true) - before) / Iterations;
        }

        Array.Sort(times);
        Array.Sort(cpuTimes);
        Array.Sort(analyzerTimes);
        Array.Sort(allocations);
        var compilerComparison = await AnalyzerCompilerComparison.Measure(
            suite, compilation, fresh => Analyze(fresh, analyzer), count).ConfigureAwait(false);
        return new AnalyzerRegressionMeasurement(suite, times[Samples / 2], cpuTimes[Samples / 2], analyzerTimes[Samples / 2],
            allocations[Samples / 2], count, compilerComparison);
    }

    private static Task<AnalysisResult> Analyze(CSharpCompilation compilation, DiagnosticAnalyzer analyzer) =>
        compilation.WithAnalyzers([analyzer], AnalysisOptions).GetAnalysisResultAsync(CancellationToken.None);
}
