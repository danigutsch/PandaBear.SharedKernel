using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>
/// An absolute <c>http</c> or <c>https</c> web address that is safe to render as a link, such as a sign-up form or a social
/// profile, for example <c>https://example.org/tours?page=2</c>. Its scheme and host are stored in lower case; the path, query
/// and fragment are kept exactly as supplied. <see cref="CreateHttps"/> accepts <c>https</c> only.
/// </summary>
public sealed class WebUrl : ValueObject
{
    /// <summary>The longest accepted address, in characters.</summary>
    public const int MaxLength = 2048;

    private const string HttpPrefix = "http://";
    private const string HttpsPrefix = "https://";

    private WebUrl(string value) => Value = value;

    /// <summary>Gets the address with a lower-case scheme and host.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a web address from text. It must start with <c>http://</c> or <c>https://</c> (in any case), name a host and carry no
    /// user information. White space or control characters anywhere (they are rejected rather than trimmed, because browsers strip
    /// some of them and that can hide a scheme), backslashes, text that is not in Unicode normalization form C, lone surrogates,
    /// relative and protocol-relative addresses, every other scheme (<c>javascript:</c>, <c>data:</c>) and addresses longer than
    /// <see cref="MaxLength"/> are rejected.
    /// </summary>
    /// <param name="value">The candidate address.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <param name="label">The label used in validation messages.</param>
    /// <returns>The address or a validation error that never echoes the input.</returns>
    public static Result<WebUrl> Create(string? value, string field = "url", string label = "Link") =>
        Create(value, field, label, allowHttp: true);

    /// <summary>
    /// Creates a web address that must use <c>https</c>, such as a video on another site or an external link that must not
    /// downgrade: the rules of <see cref="Create(string?, string, string)"/>, except that an <c>http://</c> address is rejected like
    /// every other scheme, and the messages name <c>https</c> alone.
    /// </summary>
    /// <param name="value">The candidate address.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <param name="label">The label used in validation messages.</param>
    /// <returns>The address or a validation error that never echoes the input.</returns>
    public static Result<WebUrl> CreateHttps(string? value, string field = "url", string label = "Link") =>
        Create(value, field, label, allowHttp: false);

    /// <summary>For persistence mappers: rebuilds an address that was produced by this type earlier; it performs NO validation beyond null checks, so never use it for user input.</summary>
    /// <param name="value">The stored address.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty or blank.</exception>
    public static WebUrl Restore(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new WebUrl(value);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static Result<WebUrl> Create(string? value, string field, string label, bool allowHttp)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Invalid(field, $"{label} is required.");
        }

        if (value.Length > MaxLength)
        {
            return Invalid(field, $"{label} cannot exceed {MaxLength} characters.");
        }

        if (value.Any(static character => char.IsWhiteSpace(character) || char.IsControl(character) || character == '\\'))
        {
            return Invalid(field, $"{label} cannot contain white space, control characters or backslashes.");
        }

        var schemes = allowHttp ? "http or https" : "https";
        var https = value.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase);
        if (!https && !(allowHttp && value.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            return Invalid(field, $"{label} must be an absolute {schemes} address.");
        }

        var rest = value[(https ? HttpsPrefix : HttpPrefix).Length..];
        var authorityEnd = rest.IndexOfAny(['/', '?', '#']);
        var authority = authorityEnd < 0 ? rest : rest[..authorityEnd];
        var path = authorityEnd < 0 ? string.Empty : rest[authorityEnd..];

        // The sanitizer accepts https only; an http address is checked through its https form, which has the same shape. It
        // refuses user information but lets an empty one ("https://@host") through, so any '@' in the authority is refused here.
        var candidate = HttpsPrefix + rest;
        if (authority.Contains('@', StringComparison.Ordinal)
            || !string.Equals(WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(candidate, int.MaxValue), candidate, StringComparison.Ordinal))
        {
            return Invalid(field, $"{label} must be an absolute {schemes} address with a host and no user information.");
        }

#pragma warning disable CA1308 // Lower case is the canonical form of a case-insensitive scheme and host, not a security normalization.
        return Result.Ok(new WebUrl((https ? HttpsPrefix : HttpPrefix) + authority.ToLowerInvariant() + path));
#pragma warning restore CA1308
    }

    private static Result<WebUrl> Invalid(string field, string message) =>
        Result.Invalid<WebUrl>(detail: message, field: field, message: message);
}
