using System.Globalization;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>Reads a computed CSS length, as Chromium reports it.</summary>
internal static class CssLength
{
    /// <summary>Converts a computed length in pixels, such as <c>56px</c>.</summary>
    /// <param name="computed">The computed value.</param>
    /// <returns>The length in CSS pixels.</returns>
    /// <exception cref="FormatException">The value is not a length in pixels.</exception>
    public static double Pixels(string computed)
    {
        ArgumentNullException.ThrowIfNull(computed);
        return computed.EndsWith("px", StringComparison.Ordinal)
            ? double.Parse(computed[..^2], CultureInfo.InvariantCulture)
            : throw new FormatException("The value is not a length in pixels: " + computed);
    }
}
