using System.Buffers;
using System.Globalization;
using System.Text;

namespace PandaBear.InputNormalization;

/// <summary>
/// Decides where <see cref="PrintableText"/> keeps a zero width joiner, a zero width non-joiner, or a tag character: only
/// in the sequences in which they change how the text shows, so that they can neither hide nor reorder text elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// A zero width joiner U+200D is kept between two emoji of an emoji ZWJ sequence (UTS #51 ED-15a and ED-16), and in the
/// conjunct context of UTS #39 section 3.1.1.1 condition B. A zero width non-joiner U+200C is kept where it breaks a
/// cursive connection (condition A1) and in the conjunct context of condition A2. UAX #31 moved these conditions to UTS
/// #39 in Unicode 15.1, and UTS #55 section 5.1.3 recommends applying them.
/// </para>
/// <para>
/// The conditions are approximated, and every approximation keeps fewer joiners than the standards allow, except that the
/// global script and NFC conditions of UTS #39 are not checked: a joiner between characters of different scripts is kept
/// when the characters next to it fit a condition. Both sides of a kept joiner or non-joiner are always visible
/// characters, so it is never first, last, next to white space, or next to another joiner.
/// </para>
/// </remarks>
internal static class JoinerContexts
{
    /// <summary>U+200C ZERO WIDTH NON-JOINER.</summary>
    public const char ZeroWidthNonJoiner = '\u200C';

    /// <summary>U+200D ZERO WIDTH JOINER.</summary>
    public const char ZeroWidthJoiner = '\u200D';

    /// <summary>U+1F3F4 WAVING BLACK FLAG, the only base of a valid emoji tag sequence.</summary>
    public const int BlackFlag = 0x1F3F4;

    private const int CancelTag = 0xE007F;
    private const int EmojiPresentationSelector = 0xFE0F;

    // A valid flag tag_spec is a CLDR unicode_subdivision_id, a region of two letters or three digits followed by one to
    // four letters or digits, or a three-digit unicode_region_subtag (UTS #51 Annex C, and UTS #35 unicode_subdivision_id).
    private const int MinTagSpecLength = 3;
    private const int MaxTagSpecLength = 7;

    /// <summary>Returns whether the zero width joiner or non-joiner at <paramref name="index"/> is kept.</summary>
    /// <param name="text">Text.</param>
    /// <param name="index">Index of a <see cref="ZeroWidthJoiner"/> or <see cref="ZeroWidthNonJoiner"/> in <paramref name="text"/>.</param>
    /// <returns><see langword="true"/> when the character is in a context that the standards allow.</returns>
    public static bool IsKept(string text, int index)
    {
        int after = index + 1;
        if (text[index] == ZeroWidthJoiner)
        {
            // UTS #51 ED-16: emoji_zwj_element (ZWJ emoji_zwj_element)+. UTS #39 B: /$L $M* $V $M₁* ZWJ (?!$D)/, here
            // also followed by a letter, which a dependent vowel ($D, always a mark) is not.
            return (EndsWithEmoji(text, index) && StartsWithEmoji(text, after))
                || (EndsWithConjunct(text, index) && TryRuneAt(text, after, out Rune next, out _) && Rune.IsLetter(next));
        }

        // UTS #39 A1: /$LJ $T* ZWNJ $RJ/. A2: /$L $M* $V $M₁* ZWNJ $M₁* $L/.
        return (EndsWithLeftJoining(text, index) && TryRuneAt(text, after, out Rune right, out _)
                && UnicodeProperties.IsRightJoining(right.Value))
            || (EndsWithConjunct(text, index) && StartsWithMarksAndLetter(text, after));
    }

    /// <summary>
    /// Returns the length of the well-formed emoji tag sequence that starts at <paramref name="index"/>: U+1F3F4, then three
    /// to seven tag digits U+E0030 to U+E0039 or tag small letters U+E0061 to U+E007A, then U+E007F CANCEL TAG.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <param name="index">Index in <paramref name="text"/>.</param>
    /// <returns>The length in UTF-16 code units, or 0 when no such sequence starts at <paramref name="index"/>.</returns>
    /// <remarks>
    /// UTS #51 ED-14a allows any tag_spec of U+E0020 to U+E007E after any emoji, but Annex C limits a valid tag sequence to
    /// this base and these characters, and a tag-unaware terminal shows tags as nothing. The stricter rule keeps every valid
    /// flag, such as England, Scotland, and Wales, and prevents text hidden in tags.
    /// </remarks>
    public static int EmojiTagSequenceLength(string text, int index)
    {
        if (!TryRuneAt(text, index, out Rune flag, out int position) || flag.Value != BlackFlag)
        {
            return 0;
        }

        position += index;
        int tagSpecLength = 0;
        while (TryRuneAt(text, position, out Rune tag, out int length) && IsTagSpec(tag.Value))
        {
            if (++tagSpecLength > MaxTagSpecLength)
            {
                return 0;
            }

            position += length;
        }

        if (tagSpecLength < MinTagSpecLength || !TryRuneAt(text, position, out Rune end, out int endLength) || end.Value != CancelTag)
        {
            return 0;
        }

        return position + endLength - index;
    }

    private static bool IsTagSpec(int codePoint) => codePoint is (>= 0xE0030 and <= 0xE0039) or (>= 0xE0061 and <= 0xE007A);

    private static bool IsTag(int codePoint) => codePoint is >= 0xE0020 and <= CancelTag;

    // An emoji_zwj_element that ends at end: an Extended_Pictographic character, optionally followed by emoji modifiers and
    // the emoji presentation selector, or a well-formed emoji tag sequence.
    private static bool EndsWithEmoji(string text, int end)
    {
        int position = end;
        Rune before;
        int start;
        while (TryRuneBefore(text, position, out before, out start)
            && (before.Value == EmojiPresentationSelector || UnicodeProperties.IsEmojiModifier(before.Value)))
        {
            position = start;
        }

        if (!TryRuneBefore(text, position, out before, out start))
        {
            return false;
        }

        if (before.Value == CancelTag)
        {
            int flagEnd = start;
            while (TryRuneBefore(text, flagEnd, out before, out start) && IsTag(before.Value))
            {
                flagEnd = start;
            }

            return TryRuneBefore(text, flagEnd, out _, out int flagStart)
                && EmojiTagSequenceLength(text, flagStart) == position - flagStart;
        }

        return UnicodeProperties.IsExtendedPictographic(before.Value);
    }

    // An emoji_zwj_element starts with an emoji character; one that is not Extended_Pictographic, such as a keycap or a
    // regional indicator, is not kept, as no recommended emoji ZWJ sequence joins one.
    private static bool StartsWithEmoji(string text, int start) =>
        TryRuneAt(text, start, out Rune next, out _) && UnicodeProperties.IsExtendedPictographic(next.Value);

    // A1's $LJ $T*, with $T, Joining_Type=Transparent, approximated by the nonspacing and enclosing marks; the other
    // transparent characters are format characters, which PrintableText replaces.
    private static bool EndsWithLeftJoining(string text, int end)
    {
        int position = end;
        while (TryRuneBefore(text, position, out Rune before, out int start))
        {
            if (Rune.GetUnicodeCategory(before) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark))
            {
                return UnicodeProperties.IsLeftJoining(before.Value);
            }

            position = start;
        }

        return false;
    }

    // A2's and B's $L $M* $V $M₁*, approximated as a letter followed by nonspacing marks and viramas of which at least one
    // is a virama. .NET does not expose the canonical combining class, so $M₁, the nonspacing marks with a combining class
    // other than 0, is approximated by every nonspacing mark.
    private static bool EndsWithConjunct(string text, int end)
    {
        int position = end;
        bool hasVirama = false;
        while (TryRuneBefore(text, position, out Rune before, out int start))
        {
            if (UnicodeProperties.IsVirama(before.Value))
            {
                hasVirama = true;
            }
            else if (Rune.GetUnicodeCategory(before) != UnicodeCategory.NonSpacingMark)
            {
                return hasVirama && Rune.IsLetter(before);
            }

            position = start;
        }

        return false;
    }

    // A2's $M₁* $L after the non-joiner.
    private static bool StartsWithMarksAndLetter(string text, int start)
    {
        int position = start;
        while (TryRuneAt(text, position, out Rune next, out int length))
        {
            if (Rune.GetUnicodeCategory(next) != UnicodeCategory.NonSpacingMark)
            {
                return Rune.IsLetter(next);
            }

            position += length;
        }

        return false;
    }

    private static bool TryRuneAt(string text, int index, out Rune rune, out int length)
    {
        if (index < text.Length && Rune.DecodeFromUtf16(text.AsSpan(index), out rune, out length) == OperationStatus.Done)
        {
            return true;
        }

        rune = default;
        length = 0;
        return false;
    }

    private static bool TryRuneBefore(string text, int end, out Rune rune, out int start)
    {
        if (end > 0 && Rune.DecodeLastFromUtf16(text.AsSpan(0, end), out rune, out int length) == OperationStatus.Done)
        {
            start = end - length;
            return true;
        }

        rune = default;
        start = end;
        return false;
    }
}
