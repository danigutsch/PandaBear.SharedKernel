namespace PandaBear.Offerings;

/// <summary>
/// A measured fact of an offering or a step, of a kind the application defines (for a tour: distance, elevation gain, difficulty). It is
/// also the entered value.
/// </summary>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
/// <param name="Kind">The kind, one of the rules' metrics.</param>
/// <param name="Value">The value, or the lower end of a range.</param>
/// <param name="Upper">The upper end of a range, such as difficulty 2–3; only for a kind that allows a range.</param>
/// <param name="Approximate">Whether the value is approximate; only for a kind that allows it.</param>
public sealed record Metric<TMetric>(TMetric Kind, decimal Value, decimal? Upper = null, bool Approximate = false)
    where TMetric : struct, Enum;
