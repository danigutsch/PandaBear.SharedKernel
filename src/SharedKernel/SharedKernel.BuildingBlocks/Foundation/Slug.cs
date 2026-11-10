using System.Globalization;
using System.Text;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A canonical URL slug: lowercase ASCII letters and digits joined by single hyphens, for example <c>viagem-a-patagonia</c>.</summary>
public sealed class Slug : ValueObject
{
    /// <summary>The default maximum slug length.</summary>
    public const int DefaultMaxLength = 128;

    private Slug(string value) => Value = value;

    /// <summary>Gets the canonical slug.</summary>
    public string Value
    {
        get;
    }

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
