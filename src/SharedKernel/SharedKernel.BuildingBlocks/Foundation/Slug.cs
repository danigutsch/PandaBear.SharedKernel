using System.Globalization;
using System.Text;
using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>A canonical URL slug: lowercase ASCII letters and digits joined by single hyphens, for example <c>viagem-a-patagonia</c>.</summary>
public sealed class Slug : ValueObject
{
    /// <summary>The default maximum slug length.</summary>
    public const int DefaultMaxLength = 128;

    private Slug(string value) => Value = value;

    /// <summary>Gets the canonical slug.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a slug from free text. Diacritics are stripped (<c>Patagônia</c> becomes <c>patagonia</c>), whitespace, hyphens and
    /// underscores collapse to a single hyphen, and text with control characters, surrogates (including lone ones),
    /// <c>/ \ ? #</c>, <c>.</c>, or any other non-ASCII character is rejected instead of transliterated.
    /// </summary>
    /// <param name="value">The free text.</param>
    /// <param name="maxLength">The longest allowed slug.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <param name="label">The label used in validation messages.</param>
    /// <returns>The slug or a validation error that never echoes the input.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is not positive.</exception>
    public static Result<Slug> Create(string? value, int maxLength = DefaultMaxLength, string field = "slug", string label = "Slug")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (string.IsNullOrWhiteSpace(value))
        {
            return Invalid(field, $"{label} is required.");
        }

        if (!TryBuild(value, out var slug))
        {
            return Invalid(field, $"{label} can only contain letters, digits, spaces, '-' and '_'.");
        }

        if (slug.Length == 0)
        {
            return Invalid(field, $"{label} must contain a letter or a digit.");
        }

        return slug.Length > maxLength
            ? Invalid(field, $"{label} cannot exceed {maxLength} characters.")
            : Result.Ok(new Slug(slug));
    }

    /// <summary>
    /// Creates a slug from any text, for example a file name from a trip code: diacritics are stripped as in <see cref="Create"/>
    /// (<c>ÁLPES</c> becomes <c>alpes</c>), ASCII letters are lowercased, every run of other characters (white space, punctuation such as
    /// <c>. / \ ? #</c>, control characters, surrogates and letters without an ASCII base such as <c>ß</c> or <c>東</c>) becomes a single
    /// hyphen, and leading and trailing hyphens are dropped, so <c>ÁLPES/2027 ..\x</c> becomes <c>alpes-2027-x</c>. A slug longer than
    /// <paramref name="maxLength"/> is cut there, without a trailing hyphen. The result is canonical, and text that <see cref="Create"/>
    /// accepts gives the same slug.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="maxLength">The longest slug; a longer one is cut.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <param name="label">The label used in validation messages.</param>
    /// <returns>
    /// The slug, or a validation error that never echoes the input: required for missing or blank text, and must contain a letter or a
    /// digit when no ASCII letter or digit remains (<c>中国</c>, <c>---</c>).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is not positive.</exception>
    public static Result<Slug> CreateLenient(string? value, int maxLength = DefaultMaxLength, string field = "slug", string label = "Slug")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (string.IsNullOrWhiteSpace(value))
        {
            return Invalid(field, $"{label} is required.");
        }

        var slug = BuildLenient(value);
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return slug.Length == 0
            ? Invalid(field, $"{label} must contain a letter or a digit.")
            : Result.Ok(new Slug(slug));
    }

    /// <summary>Determines whether a value is already a canonical slug, that is, creating a slug from it returns it unchanged.</summary>
    /// <param name="value">The candidate slug.</param>
    /// <param name="maxLength">The longest allowed slug.</param>
    /// <returns><see langword="true"/> when the value is canonical; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is not positive.</exception>
    public static bool IsCanonical(string? value, int maxLength = DefaultMaxLength)
    {
        var result = Create(value, maxLength);
        return result.IsSuccess && string.Equals(result.Value.Value, value, StringComparison.Ordinal);
    }

    /// <summary>For persistence mappers: rebuilds a slug that was produced by this type earlier; it performs NO validation beyond null checks, so never use it for user input.</summary>
    /// <param name="value">The stored slug.</param>
    /// <returns>The slug.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty or blank.</exception>
    public static Slug Restore(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Slug(value);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static Result<Slug> Invalid(string field, string message) =>
        Result.Invalid<Slug>(detail: message, field: field, message: message);

    private static string BuildLenient(string value)
    {
        var withoutSurrogates = string.Create(value.Length, value, static (span, text) =>
        {
            for (var index = 0; index < text.Length; index++)
            {
                span[index] = char.IsSurrogate(text[index]) ? ' ' : text[index];
            }
        });

        var builder = new StringBuilder(value.Length);
        var requiresSeparator = false;
        foreach (var character in withoutSurrogates.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                if (requiresSeparator)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                requiresSeparator = false;
            }
            else
            {
                requiresSeparator = builder.Length > 0;
            }
        }

        return builder.ToString();
    }

    private static bool TryBuild(string value, out string slug)
    {
        slug = string.Empty;
        // Reject every surrogate before Normalize, which throws on a lone one; a valid pair is non-ASCII and rejected anyway.
        if (value.Any(char.IsControl) || value.Any(char.IsSurrogate) || value.IndexOfAny(['/', '\\', '?', '#']) >= 0)
        {
            return false;
        }

        var builder = new StringBuilder(value.Length);
        var requiresSeparator = false;
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                if (requiresSeparator)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                requiresSeparator = false;
            }
            else if (char.IsWhiteSpace(character) || character is '-' or '_')
            {
                requiresSeparator = builder.Length > 0;
            }
            else
            {
                return false;
            }
        }

        slug = builder.ToString();
        return true;
    }
}
