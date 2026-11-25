using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis.CSharp;
using System.Globalization;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Reports the total generated source size emitted for each discovery benchmark case.
/// </summary>
internal sealed class DiscoveryGeneratedSourceSizeColumn : IColumn
{
    /// <inheritdoc />
    public string Id => nameof(DiscoveryGeneratedSourceSizeColumn);

    /// <inheritdoc />
    public string ColumnName => "Generated source size";

    /// <inheritdoc />
    public bool AlwaysShow => true;

    /// <inheritdoc />
    public ColumnCategory Category => ColumnCategory.Custom;

    /// <inheritdoc />
    public int PriorityInCategory => 1;

    /// <inheritdoc />
    public bool IsNumeric => true;

    /// <inheritdoc />
    public UnitType UnitType => UnitType.Size;

    /// <inheritdoc />
    public string Legend => "Total generated source length in characters emitted by the mediator generator.";

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var compilation = CreateCompilationForScenario(benchmarkCase);
        var sourceLength = BenchmarkCompilationFactory.GetTotalGeneratedSourceLength(compilation);
        return sourceLength.ToString("N0", CultureInfo.InvariantCulture);
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
