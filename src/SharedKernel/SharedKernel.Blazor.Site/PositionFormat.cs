using System.Globalization;

namespace SharedKernel.Blazor.Site;

/// <summary>Checks a composite format that writes a 1-based position as <c>{0}</c>, such as <c>Day {0}</c> or <c>step-{0}</c>.</summary>
public static class PositionFormat
{
    /// <summary>
    /// Refuses a blank format, a format that <see cref="string.Format(IFormatProvider, string, object)"/> refuses, and a format that writes
    /// positions 1 and 2 the same, such as one without <c>{0}</c> or with an escaped <c>{{0}}</c>.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <param name="culture">The culture the format is applied with.</param>
    /// <returns>The format applied to position 1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> (named <paramref name="parameterName"/>) or <paramref name="culture"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="format"/> is blank, is not a valid format, or writes positions 1 and 2 the same;
    /// a <see cref="FormatException"/> is its inner exception.</exception>
    public static string Require(string? format, string parameterName, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(format, parameterName);
        ArgumentNullException.ThrowIfNull(culture);
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException("The format must hold {0} for the position.", parameterName);
        }

        string first;
        string second;
        try
        {
            first = string.Format(culture, format, 1);
            second = string.Format(culture, format, 2);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The format must format the position as {0} alone.", parameterName, exception);
        }

        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            throw new ArgumentException("The format must hold {0} for the position, so each step reads differently.", parameterName);
        }

        return first;
    }
}
