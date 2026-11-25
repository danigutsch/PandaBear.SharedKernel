using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis.CSharp;
using System.Globalization;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Reports the number of generated source files emitted for each discovery benchmark case.
/// </summary>
internal sealed class DiscoveryGeneratedSourceCountColumn : IColumn
{
    /// <inheritdoc />
    public string Id => nameof(DiscoveryGeneratedSourceCountColumn);

    /// <inheritdoc />
    public string ColumnName => "Generated source count";

    /// <inheritdoc />
    public bool AlwaysShow => true;

    /// <inheritdoc />
    public ColumnCategory Category => ColumnCategory.Custom;

    /// <inheritdoc />
    public int PriorityInCategory => 0;

    /// <inheritdoc />
    public bool IsNumeric => true;

    /// <inheritdoc />
    public UnitType UnitType => UnitType.Dimensionless;

    /// <inheritdoc />
    public string Legend => "Generated source file count emitted by the mediator generator.";

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var compilation = CreateCompilationForScenario(benchmarkCase);
        var sourceCount = BenchmarkCompilationFactory.GetGeneratedSourceCount(compilation);
        return sourceCount.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        return GetValue(summary, benchmarkCase);
    }

    /// <inheritdoc />
    public bool IsAvailable(Summary summary)
    {
        return true;
    }

    /// <inheritdoc />
    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase)
    {
        return false;
    }

    private static CSharpCompilation CreateCompilationForScenario(BenchmarkCase benchmarkCase)
    {
        var requestCount = Convert.ToInt32(
            benchmarkCase.Parameters[nameof(DiscoveryBenchmarks.RequestCount)],
            CultureInfo.InvariantCulture);
        var scenario = benchmarkCase.Descriptor.WorkloadMethodDisplayInfo;
        var editFirstHandler = string.Equals(scenario, "One-handler edit rebuild", StringComparison.Ordinal);
        var source = DiscoveryBenchmarkSourceFactory.CreateSource(requestCount, editFirstHandler);
        return BenchmarkCompilationFactory.CreateCompilation(source);
    }
}
