using System.Globalization;

namespace PandaBear.InputNormalization.Tests;

/// <summary>
/// The code points that <see cref="PrintableText"/> must neutralize, and a check of every code point against them.
/// </summary>
internal static class PrintableTextSamples
{
    private const int MaxReported = 40;

    /// <summary>
    /// Returns whether the code point must not reach the output: it is in the general category Cc, Cf, Zl, Zp, or Cs, or
    /// it has the Unicode property Default_Ignorable_Code_Point (DerivedCoreProperties.txt, Unicode 18.0) and is not a
    /// variation selector. A zero width joiner or non-joiner or a tag character is kept only next to particular
    /// characters, which a Latin letter on each side is not, so it must be neutralized here too.
    /// </summary>
    /// <param name="codePoint">Code point, a surrogate included.</param>
    /// <returns><see langword="true"/> when the code point must be neutralized.</returns>
    public static bool MustBeNeutralized(int codePoint)
    {
        if (codePoint is >= 0xD800 and <= 0xDFFF)
        {
            return true;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
        if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator)
        {
            return true;
        }

        // The Default_Ignorable_Code_Point code points outside Cf, other than the variation selectors U+180B to U+180D,
        // U+180F, U+FE00 to U+FE0F, and U+E0100 to U+E01EF.
        return codePoint is 0x034F or 0x115F or 0x1160 or 0x17B4 or 0x17B5 or 0x2065 or 0x3164 or 0xFFA0
            or (>= 0xFFF0 and <= 0xFFF8)
            or (>= 0xE0000 and <= 0xE00FF)
            or (>= 0xE01F0 and <= 0xE0FFF);
    }

    /// <summary>Returns the text of one code point; a surrogate is a single, unpaired UTF-16 code unit.</summary>
    /// <param name="codePoint">Code point.</param>
    /// <returns>The text.</returns>
    public static string Text(int codePoint) =>
        codePoint is >= 0xD800 and <= 0xDFFF ? ((char)codePoint).ToString() : char.ConvertFromUtf32(codePoint);

    /// <summary>
    /// Prints every code point from U+0000 to U+10FFFF between two letters and returns the code points that print wrongly:
    /// a code point that <see cref="MustBeNeutralized"/> must become one space when it breaks a line or is a tab and one
    /// <see cref="PrintableText.Replacement"/> otherwise, and every other code point must stay as it is.
    /// </summary>
    /// <returns>Up to 40 descriptions of wrongly printed code points; empty when every code point prints as expected.</returns>
    public static IReadOnlyList<string> WronglyPrinted()
    {
        var wrong = new List<string>();
        for (int codePoint = 0; codePoint <= 0x10FFFF && wrong.Count < MaxReported; codePoint++)
        {
            string text = "a" + Text(codePoint) + "b";
            string expected = !MustBeNeutralized(codePoint)
                ? text
                : codePoint is '\t' or '\n' or '\r' or 0x2028 or 0x2029 ? "a b" : "a" + PrintableText.Replacement + "b";
            string printed = PrintableText.OneLine(text);
            if (!string.Equals(printed, expected, StringComparison.Ordinal))
            {
                wrong.Add(string.Create(CultureInfo.InvariantCulture, $"U+{codePoint:X4} printed as {Escape(printed)}"));
            }
        }

        return wrong;
    }

    private static string Escape(string text) =>
        string.Concat(text.Select(unit => unit is >= ' ' and <= '~' ? unit.ToString() : $"\\u{(int)unit:X4}"));
}
