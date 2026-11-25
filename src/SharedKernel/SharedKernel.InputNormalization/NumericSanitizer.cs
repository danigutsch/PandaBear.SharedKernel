namespace SharedKernel.InputNormalization;

/// <summary>
/// Provides numeric sanitization methods for domain inputs.
/// </summary>
public static class NumericSanitizer
{
    /// <summary>
    /// Sanitizes a decimal price by rounding to 2 decimal places.
    /// </summary>
    /// <param name="value">The price to sanitize.</param>
    /// <returns>The sanitized price rounded to 2 decimal places.</returns>
    public static decimal SanitizePrice(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
