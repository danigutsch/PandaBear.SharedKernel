using PandaBear.BuildingBlocks;
using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A link address that is safe to write into <c>href</c>: a relative address, an absolute <c>http</c> or <c>https</c> address
/// with a host and no user information, a <c>mailto:</c> link to one address without a query, or a <c>tel:</c> link of digits.
/// White space, control characters, backslashes, protocol-relative addresses, other schemes and addresses longer than
/// <see cref="MaxLength" /> are refused. Only <see cref="IsImageSource" /> targets are accepted as image sources.
/// </summary>
public sealed class LinkTarget : ValueObject
{
    /// <summary>The longest accepted address, in characters.</summary>
    public const int MaxLength = 2048;

    private const string HttpScheme = "http";
    private const string HttpsScheme = "https";

    private LinkTarget(string value, bool isImageSource)
    {
        Value = value;
        IsImageSource = isImageSource;
    }

    /// <summary>Gets the address exactly as supplied.</summary>
    public string Value
    {
        get;
    }

    /// <summary>
    /// Gets whether the address is root-relative (<c>/img/a.jpg</c>) or absolute <c>https</c>, the only shapes accepted for
    /// <c>Hero.ImageSrc</c>, <c>Card.ImageSrc</c> and <c>GalleryImage.Src</c>.
    /// </summary>
    public bool IsImageSource
    {
        get;
    }

    /// <summary>Creates a link target from text.</summary>
    /// <param name="value">The candidate address.</param>
    /// <returns>The target, or an input-independent validation error.</returns>
    public static Result<LinkTarget> Create(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.All(char.IsWhiteSpace))
        {
            return Invalid("A link address is required.");
        }

        if (value.Length > MaxLength)
        {
            return Invalid($"A link address must not be longer than {MaxLength} characters.");
        }

        if (value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) || character == '\\'))
        {
            return Invalid("A link address must not contain white space, control characters or backslashes.");
        }

        var schemeEnd = value.IndexOfAny([':', '/', '?', '#']);
        if (schemeEnd <= 0 || value[schemeEnd] != ':')
        {
            return Relative(value);
        }

        var scheme = value[..schemeEnd];
        var rest = value[(schemeEnd + 1)..];
        return scheme.ToUpperInvariant() switch
        {
            "HTTP" or "HTTPS" => Web(value, scheme),
            "MAILTO" => IsSingleAddress(rest) ? Result.Ok(new LinkTarget(value, false)) : Invalid("A mailto link must name one address and carry no query or headers."),
            "TEL" => IsTelephone(rest) ? Result.Ok(new LinkTarget(value, false)) : Invalid("A tel link must be digits with an optional leading plus sign."),
            _ => Invalid("Only relative addresses and the http, https, mailto and tel schemes are allowed."),
        };
    }

    /// <summary>Creates a link target from text that the author controls, such as a literal in markup.</summary>
    /// <param name="value">The address.</param>
    /// <returns>The target.</returns>
    /// <exception cref="ArgumentException"><paramref name="value" /> is not a safe link address.</exception>
    public static LinkTarget FromString(string value)
    {
        var created = Create(value);
        return created.TryGetValue(out var target) ? target : throw new ArgumentException(created.ErrorDetails?.Detail, nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static Result<LinkTarget> Relative(string value)
    {
        if (value[0] != '/')
        {
            // A path, query or fragment relative to the current page, for example "tours", "?page=2" or "#top".
            return Result.Ok(new LinkTarget(value, false));
        }

        return IsAcceptedUnchanged(value)
            ? Result.Ok(new LinkTarget(value, true))
            : Invalid("A protocol-relative link address is not allowed.");
    }

    private static Result<LinkTarget> Web(string value, string scheme)
    {
        if (!value.AsSpan(scheme.Length).StartsWith("://", StringComparison.Ordinal))
        {
            return Invalid("An http or https link address must be absolute, with // and a host.");
        }

        // The sanitizer accepts https only; an http address is checked through its https form, which has the same shape.
        var https = string.Equals(scheme, HttpsScheme, StringComparison.OrdinalIgnoreCase);
        var candidate = https ? value : HttpsScheme + value[HttpScheme.Length..];
        return IsAcceptedUnchanged(candidate)
            ? Result.Ok(new LinkTarget(value, https))
            : Invalid("An http or https link address needs a host and must not carry user information.");
    }

    private static bool IsAcceptedUnchanged(string value) =>
        string.Equals(WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(value, MaxLength), value, StringComparison.Ordinal);

    private static bool IsSingleAddress(string address)
    {
        var at = address.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && at < address.Length - 1
            && address.IndexOf('@', at + 1) < 0
            && address.IndexOfAny(['?', '&', ';', ',', '%', '/', ':', '<', '>', '"']) < 0;
    }

    private static bool IsTelephone(string number)
    {
        var digits = number.StartsWith('+') ? number[1..] : number;
        return digits.Length > 0 && digits.All(char.IsAsciiDigit);
    }

    private static Result<LinkTarget> Invalid(string message) =>
        Result.Invalid<LinkTarget>(detail: message, field: "href", message: message);
}
