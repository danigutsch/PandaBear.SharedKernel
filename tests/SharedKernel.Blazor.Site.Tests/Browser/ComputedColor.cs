using System.Globalization;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>Converts a computed CSS colour, as Chromium reports it, to the <c>#rrggbb</c> form the contrast math reads.</summary>
internal static class ComputedColor
{
    /// <summary>Converts an opaque computed colour such as <c>rgb(29, 78, 216)</c>.</summary>
    /// <param name="computed">The computed value.</param>
    /// <returns>The colour as lower-case <c>#rrggbb</c>.</returns>
    /// <exception cref="FormatException">The value is not an opaque <c>rgb()</c> colour.</exception>
    public static string ToHex(string computed)
    {
        ArgumentNullException.ThrowIfNull(computed);
        if (!computed.StartsWith("rgb(", StringComparison.Ordinal))
        {
            throw new FormatException("The colour is not an opaque rgb() colour: " + computed);
        }

        var channels = computed[4..^1].Split(',', StringSplitOptions.TrimEntries).Select(channel => int.Parse(channel, CultureInfo.InvariantCulture));
        return "#" + string.Concat(channels.Select(channel => channel.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
