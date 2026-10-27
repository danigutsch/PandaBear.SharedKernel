using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PandaBear.Aspire.Analyzers;
using PandaBear.Mediator.Analyzers;
using PandaBear.Style.Analyzers;
using PandaBear.Testing.Analyzers;

namespace PandaBear.Mediator.Benchmarks;

/// <summary>Collects or checks analyzer performance baselines for the mediator analyzers.</summary>
internal static class AnalyzerRegressionCommand
{
    private const int SourceCount = 100;
    private const int Samples = 9;
    private const int Iterations = 5;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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

        var suites = new (DiagnosticAnalyzer Analyzer, string Source)[]
        {
            (new SharedKernelMediatorAnalyzer(), AnalyzerBenchmarkSourceFactory.CreateSource(SourceCount, AnalyzerBenchmarkSourceFactory.ManyDiagnostics)),
            (new SharedKernelStyleAnalyzer(), string.Join(Environment.NewLine, Enumerable.Range(0, SourceCount).Select(static i => $"public class Clock{i.ToString(CultureInfo.InvariantCulture)} {{ public object ReadAsync() => DateTimeOffset.UtcNow; }}"))),
            (new SharedKernelTestingAnalyzer(), "namespace Xunit { public class FactAttribute : Attribute {} } namespace Cases {" + string.Join(Environment.NewLine, Enumerable.Range(0, SourceCount).Select(static i => $"public class Case{i.ToString(CultureInfo.InvariantCulture)} {{ [Xunit.Fact] public void BadName() {{ var sut = new object(); }} }}")) + "}"),
            (new SharedKernelAspireAnalyzer(), "public class Resource { public Resource WithImageTag(string tag) => this; } public class Host { public void Build(Resource resource) {" + string.Concat(Enumerable.Repeat("resource.WithImageTag(\"latest\");", SourceCount)) + "} }"),
        };
        var measurements = new List<AnalyzerRegressionMeasurement>();
        foreach (var (analyzer, source) in suites)
        {
            var compilation = BenchmarkCompilationFactory.CreateCompilation(source);
            var errors = compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length != 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(static d => d.ToString())));
            }

            for (var warmup = 0; warmup < 10; warmup++)
            {
                _ = await Analyze(compilation, analyzer).ConfigureAwait(false);
            }

            using var process = Process.GetCurrentProcess();
            var times = new double[Samples];
            var cpuTimes = new double[Samples];
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
                ImmutableArray<Diagnostic> diagnostics = [];
                for (var iteration = 0; iteration < Iterations; iteration++)
                {
                    diagnostics = await Analyze(compilation, analyzer).ConfigureAwait(false);
                }
                process.Refresh();
                cpuTimes[sample] = (process.TotalProcessorTime - cpuStart).TotalMilliseconds / Iterations;
                times[sample] = Stopwatch.GetElapsedTime(start).TotalMilliseconds / Iterations;
                allocations[sample] = (GC.GetTotalAllocatedBytes(precise: true) - before) / Iterations;
                if (diagnostics.Any(static d => d.Id == "AD0001") || diagnostics.Length == 0 || count >= 0 && count != diagnostics.Length)
                {
                    throw new InvalidOperationException("Analyzer benchmark did not produce stable, nonempty diagnostics: " + analyzer.GetType().Name);
                }

                count = diagnostics.Length;
            }

            Array.Sort(times);
            Array.Sort(cpuTimes);
            Array.Sort(allocations);
            var measurement = new AnalyzerRegressionMeasurement(analyzer.GetType().Name, times[Samples / 2], cpuTimes[Samples / 2], allocations[Samples / 2], count);
            measurements.Add(measurement);
            Console.WriteLine(JsonSerializer.Serialize(measurement));
        }

        var actual = new AnalyzerRegressionBaseline(1, RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(), SourceCount, Samples, Iterations, 1.5, 1.2, [.. measurements]);
        if (mode == "collect")
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(actual, JsonOptions) + Environment.NewLine).ConfigureAwait(false);
            return 0;
        }

        var baseline = JsonSerializer.Deserialize<AnalyzerRegressionBaseline>(await File.ReadAllTextAsync(path).ConfigureAwait(false))
            ?? throw new InvalidOperationException("Missing analyzer baseline.");
        if (baseline.SchemaVersion != actual.SchemaVersion || baseline.Runtime != actual.Runtime || baseline.OperatingSystem != actual.OperatingSystem
            || baseline.Architecture != actual.Architecture || baseline.SourceCount != SourceCount || baseline.Samples != Samples || baseline.Iterations != Iterations
            || baseline.MaximumTimeRatio != actual.MaximumTimeRatio || baseline.MaximumAllocationRatio != actual.MaximumAllocationRatio
            || baseline.Measurements.Length != measurements.Count)
        {
            await Console.Error.WriteLineAsync("Incomparable analyzer baseline: use the recorded runtime, OS, architecture, corpus and sample count.").ConfigureAwait(false);
            return 2;
        }

        var failed = false;
        foreach (var measurement in measurements)
        {
            var expected = baseline.Measurements.SingleOrDefault(item => item.Suite == measurement.Suite);
            if (expected is null || !double.IsFinite(expected.CpuMilliseconds) || expected.CpuMilliseconds <= 0 || expected.AllocatedBytes <= 0
                || expected.Diagnostics != measurement.Diagnostics || measurement.CpuMilliseconds > expected.CpuMilliseconds * baseline.MaximumTimeRatio
                || measurement.AllocatedBytes > expected.AllocatedBytes * baseline.MaximumAllocationRatio)
            {
                await Console.Error.WriteLineAsync("Analyzer regression threshold exceeded or baseline invalid: " + measurement.Suite).ConfigureAwait(false);
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }

    private static Task<ImmutableArray<Diagnostic>> Analyze(CSharpCompilation compilation, DiagnosticAnalyzer analyzer) =>
        compilation.WithAnalyzers([analyzer], new CompilationWithAnalyzersOptions(new AnalyzerOptions([]), null,
            concurrentAnalysis: false, logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: false)).GetAnalyzerDiagnosticsAsync();
}
