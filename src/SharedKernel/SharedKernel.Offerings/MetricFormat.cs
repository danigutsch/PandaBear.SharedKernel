namespace SharedKernel.Offerings;

/// <summary>The application's format for one metric kind (see <see cref="OfferingText.Metric{TMetric}"/>).</summary>
/// <param name="Pattern">The format of the number, <c>{0}</c>, with its unit, such as <c>{0} km</c>.</param>
/// <param name="ApproximatePattern">The format that marks an approximate value, <c>{0}</c> being the formatted pattern, such as <c>aprox. {0}</c>.</param>
/// <param name="Decimals">The most decimal places shown; trailing zeros are left out.</param>
public sealed record MetricFormat(string Pattern, string ApproximatePattern, int Decimals = 0)
{
    /// <summary>Gets the format of the number with its unit.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string Pattern { get; } = FormatText.Required(Pattern, nameof(Pattern));

    /// <summary>Gets the format that marks an approximate value.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string ApproximatePattern { get; } = FormatText.Required(ApproximatePattern, nameof(ApproximatePattern));

    /// <summary>Gets the most decimal places shown.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or above 28.</exception>
    public int Decimals { get; } = Decimals is >= 0 and <= 28 ? Decimals : throw new ArgumentOutOfRangeException(nameof(Decimals), Decimals, "Decimals must be 0 to 28.");
}
