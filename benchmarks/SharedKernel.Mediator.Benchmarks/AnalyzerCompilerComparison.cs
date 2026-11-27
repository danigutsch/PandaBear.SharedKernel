using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>Paired compiler measurements for one analyzer and synthetic workload.</summary>
/// <param name="CompilerOnlyMilliseconds">Median elapsed compiler time without diagnostic analyzers.</param>
/// <param name="CompilerAndAnalyzerMilliseconds">Median elapsed compiler and analyzer time.</param>
/// <param name="MedianPairedElapsedRatio">Median of individual compiler-plus-analyzer to compiler-only elapsed time ratios.</param>
internal sealed record AnalyzerCompilerComparison(
    double CompilerOnlyMilliseconds,
    double CompilerAndAnalyzerMilliseconds,
    double? MedianPairedElapsedRatio)
{
    private const int PairCount = 5;

    /// <summary>Measures the two arms with fresh compilations from identical inputs on each sample.</summary>
    /// <param name="suite">The analyzer and source workload name.</param>
    /// <param name="template">The parsed syntax trees, compilation options, and metadata references.</param>
    /// <param name="analyze">The diagnostic analyzer pass for the treatment arm.</param>
    /// <param name="expectedDiagnostics">The diagnostic count produced by the standard warm measurement.</param>
    public static async Task<AnalyzerCompilerComparison> Measure(
        string suite,
        CSharpCompilation template,
        Func<CSharpCompilation, Task<AnalysisResult>> analyze,
        int expectedDiagnostics)
    {
        _ = await MeasureArm(suite, template, analyze: null, expectedDiagnostics).ConfigureAwait(false);
        _ = await MeasureArm(suite, template, analyze, expectedDiagnostics).ConfigureAwait(false);

        var compilerOnly = new double[PairCount];
        var compilerAndAnalyzer = new double[PairCount];
        var ratios = new List<double>(PairCount);
        for (var sample = 0; sample < PairCount; sample++)
        {
            if (sample % 2 == 0)
            {
                compilerOnly[sample] = await MeasureArm(suite, template, analyze: null, expectedDiagnostics).ConfigureAwait(false);
                compilerAndAnalyzer[sample] = await MeasureArm(suite, template, analyze, expectedDiagnostics).ConfigureAwait(false);
            }
            else
            {
                compilerAndAnalyzer[sample] = await MeasureArm(suite, template, analyze, expectedDiagnostics).ConfigureAwait(false);
                compilerOnly[sample] = await MeasureArm(suite, template, analyze: null, expectedDiagnostics).ConfigureAwait(false);
            }

            if (compilerOnly[sample] > 0)
            {
                ratios.Add(compilerAndAnalyzer[sample] / compilerOnly[sample]);
            }
        }

        Array.Sort(compilerOnly);
        Array.Sort(compilerAndAnalyzer);
        ratios.Sort();
        return new AnalyzerCompilerComparison(
            compilerOnly[PairCount / 2],
            compilerAndAnalyzer[PairCount / 2],
            ratios.Count == 0 ? null : (ratios[(ratios.Count - 1) / 2] + ratios[ratios.Count / 2]) / 2);
    }

    private static async Task<double> MeasureArm(
        string suite,
        CSharpCompilation template,
        Func<CSharpCompilation, Task<AnalysisResult>>? analyze,
        int expectedDiagnostics)
    {
        var start = Stopwatch.GetTimestamp();
        var compilation = CSharpCompilation.Create(template.AssemblyName, template.SyntaxTrees, template.References, template.Options);
        var emit = compilation.Emit(Stream.Null);
        var analysis = emit.Success && analyze is not null ? await analyze(compilation).ConfigureAwait(false) : null;
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        if (!emit.Success)
        {
            var errors = emit.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            throw new InvalidOperationException(suite + ": " + string.Join(Environment.NewLine, errors));
        }

        if (analysis is not null)
        {
            var diagnostics = analysis.GetAllDiagnostics();
            if (diagnostics.Length != expectedDiagnostics || diagnostics.Any(static diagnostic => diagnostic.Id == "AD0001"))
            {
                throw new InvalidOperationException("The compiler comparison did not produce the measured analyzer diagnostics: " + suite);
            }
        }

        return elapsed;
    }
}
