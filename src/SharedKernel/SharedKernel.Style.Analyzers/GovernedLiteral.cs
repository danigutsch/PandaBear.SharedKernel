namespace SharedKernel.Style.Analyzers;

/// <summary>A catalogued literal and its canonical or compatibility declaration.</summary>
/// <param name="value">The exact governed value.</param>
/// <param name="assembly">The simple name of the declaring assembly.</param>
/// <param name="symbol">The fully qualified field name.</param>
/// <param name="justification">The reason for a compatibility exception, or null for a canonical declaration.</param>
internal sealed class GovernedLiteral(string value, string assembly, string symbol, string? justification)
{
    /// <summary>Gets the exact governed value.</summary>
    public string Value { get; } = value;

    /// <summary>Gets the simple name of the declaring assembly.</summary>
    public string Assembly { get; } = assembly;

    /// <summary>Gets the fully qualified field name.</summary>
    public string Symbol { get; } = symbol;

    /// <summary>Gets the reason for a compatibility exception, or null for a canonical declaration.</summary>
    public string? Justification { get; } = justification;
}
