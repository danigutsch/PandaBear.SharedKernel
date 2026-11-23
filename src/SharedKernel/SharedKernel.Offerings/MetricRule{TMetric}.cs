namespace PandaBear.Offerings;

/// <summary>The application's rule for one metric kind. The order of the rules is the display order of the metrics.</summary>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
/// <param name="Kind">The kind.</param>
/// <param name="Scope">Where the metric may be given.</param>
/// <param name="Min">The smallest value.</param>
/// <param name="Max">The largest value, also of the upper end of a range.</param>
/// <param name="Decimals">The most decimal places.</param>
/// <param name="AllowsRange">Whether an upper value may make it a range, such as difficulty 2–3.</param>
/// <param name="AllowsApproximate">Whether it may be marked approximate.</param>
public sealed record MetricRule<TMetric>(
    TMetric Kind,
    MetricScope Scope,
    decimal Min,
    decimal Max,
    int Decimals = 0,
    bool AllowsRange = false,
    bool AllowsApproximate = false)
    where TMetric : struct, Enum;
