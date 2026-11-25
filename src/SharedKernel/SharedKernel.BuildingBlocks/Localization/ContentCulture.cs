using System.Text;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The culture of a piece of content, such as <c>pt</c> or <c>pt-BR</c>, written as a path-safe language tag in canonical casing
/// so cultures that differ only in case are the same culture. Casing follows BCP 47: the language is lower case, and before the
/// first single-character subtag (an extension such as <c>-u-</c> or the private use <c>-x-</c>) a four-letter script is title case
/// and a two-letter region upper case; everything from that singleton on is lower case (<c>sr-Latn-RS-u-nu-latn</c>).
/// It is a name, not a <see cref="System.Globalization.CultureInfo"/>, so it does not depend on the host's globalization data.
/// It is a path-safe culture-name check (hyphen-separated ASCII letters and digits, a two-to-eight-letter first part, later subtags
/// of one to eight characters, a singleton followed by at least one subtag that is not a singleton, at most <see cref="MaxLength"/>
/// characters), not full BCP 47 validation: it does not check subtag order or registry entries.
/// </summary>
public sealed class ContentCulture : ValueObject
{
    /// <summary>The longest allowed culture name.</summary>
    public const int MaxLength = 35;

    private ContentCulture(string value) => Value = value;

    /// <summary>Gets the canonical culture name.</summary>
    public string Value { get; }

    /// <summary>
    /// Gets the culture with the last part removed (<c>pt-BR</c> gives <c>pt</c>), together with a singleton left without a subtag
    /// (<c>en-x-ab</c> gives <c>en</c>), or <see langword="null"/> for a neutral culture.
    /// </summary>
    public ContentCulture? Parent
    {
        get
        {
            var parent = Value;
            do
            {
                var index = parent.LastIndexOf('-', StringComparison.Ordinal);
                if (index <= 0)
                {
                    return null;
                }

                parent = parent[..index];
            }
            while (!IsValid(parent));

            return new ContentCulture(parent);
        }
    }

    /// <summary>Gets the language part alone (<c>pt-BR</c> gives <c>pt</c>); a neutral culture is its own language.</summary>
    public ContentCulture Language
    {
        get
        {
            var index = Value.IndexOf('-', StringComparison.Ordinal);
            return index > 0 ? new ContentCulture(Value[..index]) : this;
        }
    }

    /// <summary>Creates a culture from text; surrounding whitespace is removed and the casing is made canonical.</summary>
    /// <param name="value">The candidate culture name: hyphen-separated ASCII letters and digits whose first part is two to eight letters.</param>
    /// <returns>The culture or a validation error that never echoes the input.</returns>
    public static Result<ContentCulture> Create(string? value)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            return Invalid("Culture is required.");
        }

        return IsValid(candidate)
            ? Result.Ok(new ContentCulture(Canonicalize(candidate)))
            : Invalid("Culture must be a language tag such as 'en' or 'es-419'.");
    }

    /// <summary>For persistence mappers: rebuilds a culture that was produced by this type earlier; it performs NO validation beyond null checks and the same trimming and casing as <see cref="Create"/>, so never use it for user input.</summary>
    /// <param name="value">The stored culture name.</param>
    /// <returns>The culture.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty or blank.</exception>
    public static ContentCulture Restore(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new ContentCulture(Canonicalize(value.Trim()));
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static Result<ContentCulture> Invalid(string message) =>
        Result.Invalid<ContentCulture>(detail: message, field: "culture", message: message);

    private static bool IsValid(string candidate)
    {
        if (candidate.Length > MaxLength)
        {
            return false;
        }

        var parts = candidate.Split('-');
        if (parts[0].Length is < 2 or > 8 || !parts[0].All(char.IsAsciiLetter))
        {
            return false;
        }

        var privateUse = false;
        for (var index = 1; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Length is < 1 or > 8 || !part.All(char.IsAsciiLetterOrDigit))
            {
                return false;
            }

            if (privateUse || part.Length > 1)
            {
                continue;
            }

            if (index == parts.Length - 1 || (!IsPrivateUse(part) && parts[index + 1].Length < 2))
            {
                return false;
            }

            privateUse = IsPrivateUse(part);
        }

        return true;
    }

    private static bool IsPrivateUse(string singleton) => singleton is "x" or "X";

#pragma warning disable CA1308 // Lower case is the canonical form of a language tag, not a security normalization.
    private static string Canonicalize(string candidate)
    {
        var builder = new StringBuilder(candidate.Length);
        var parts = candidate.Split('-');
        var afterSingleton = false;
        for (var index = 0; index < parts.Length; index++)
        {
            if (index > 0)
            {
                builder.Append('-');
            }

            var part = parts[index];

            // BCP 47 casing: from the first singleton (an extension or private-use introducer) on, every subtag is lower case.
            afterSingleton |= index > 0 && part.Length == 1;
            var isRegion = !afterSingleton && index > 0 && part.Length == 2 && part.All(char.IsAsciiLetter);
            var isScript = !afterSingleton && index > 0 && part.Length == 4 && part.All(char.IsAsciiLetter);
            if (isRegion)
            {
                builder.Append(part.ToUpperInvariant());
            }
            else if (isScript)
            {
                builder.Append(char.ToUpperInvariant(part[0])).Append(part[1..].ToLowerInvariant());
            }
            else
            {
                builder.Append(part.ToLowerInvariant());
            }
        }

        return builder.ToString();
    }
#pragma warning restore CA1308
}
