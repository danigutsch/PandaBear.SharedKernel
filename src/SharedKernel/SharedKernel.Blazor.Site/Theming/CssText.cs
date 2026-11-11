using System.Globalization;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>Formats the numbers and strings that the theme writes into CSS, independent of the current culture.</summary>
internal static class CssText
{
    /// <summary>Rounds a number to the four decimals that the stylesheet writes.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The rounded number.</returns>
    public static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>Writes a number with at most four decimals and no trailing zeros, such as <c>1.6667</c> or <c>2</c>.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string Number(decimal value) => Round(value).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes a double-quoted CSS string. The theme value objects only admit text without quotes, backslashes, control characters
    /// and angle brackets; this escapes them anyway, so a string can never end early or close a <c>style</c> element.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The quoted string.</returns>
    public static string Quoted(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            if (character is '"' or '\\' or '<' or '>' || char.IsControl(character))
            {
                builder.Append('\\').Append(((int)character).ToString("x", CultureInfo.InvariantCulture)).Append(' ');
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.Append('"').ToString();
    }
}
