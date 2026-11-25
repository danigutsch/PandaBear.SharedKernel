using System.Globalization;
using System.Text;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The accent- and case-insensitive search key of a text, for comparing user search text with stored text: <c>ESLOVÊNIA</c> and
/// <c>Eslovênia</c> both become <c>eslovenia</c>.
/// </summary>
/// <remarks>
/// <para>
/// The key is built in this order, and the order is part of its definition: a lone UTF-16 surrogate becomes U+FFFD (a valid pair stays);
/// the text is decomposed canonically (Unicode NFD); nonspacing, spacing combining and enclosing marks are dropped (accents, but also
/// the vowel signs of scripts such as Devanagari); the rest is lowercased with the invariant culture; every run of white space becomes
/// one space and none is kept at either end; and the result is composed again (NFC). Every letter and digit of every script stays, and
/// so do punctuation and symbols: <c>  São   Paulo</c> becomes <c>sao paulo</c>, <c>Москва</c> becomes <c>москва</c>, and <c>東京</c>
/// and <c>100%</c> stay as they are.
/// </para>
/// <para>
/// Lowercasing is a per-character mapping, not full Unicode case folding, which .NET does not offer: <c>ß</c> does not become
/// <c>ss</c>, and compatibility forms such as full-width letters are not folded to their plain letters. The key depends on the Unicode
/// data of the running .NET; rebuild any key that is stored or indexed when the runtime changes. A key is its own key.
/// </para>
/// </remarks>
public static class SearchKey
{
    /// <summary>Computes the search key of a text.</summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <returns>The key; empty for <see langword="null"/>, empty or white-space text.</returns>
    public static string Of(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = ReplaceLoneSurrogates(text).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var space = false;
        foreach (var rune in decomposed.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (Rune.IsWhiteSpace(rune))
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(Rune.ToLowerInvariant(rune));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string ReplaceLoneSurrogates(string text)
    {
        if (!text.Any(char.IsSurrogate))
        {
            return text;
        }

        return string.Create(text.Length, text, static (span, value) =>
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var paired = char.IsHighSurrogate(character)
                    ? index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])
                    : char.IsLowSurrogate(character) && index > 0 && char.IsHighSurrogate(value[index - 1]);
                span[index] = char.IsSurrogate(character) && !paired ? '�' : character;
            }
        });
    }
}
