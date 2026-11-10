using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A link address that is safe to write into <c>href</c> and <c>src</c>: relative, or with the <c>http</c>, <c>https</c>,
/// <c>mailto</c> or <c>tel</c> scheme. Script and data schemes, protocol-relative addresses, backslashes and control characters are refused.
/// </summary>
public sealed class LinkTarget : ValueObject
{
    private static readonly string[] Schemes = ["http", "https", "mailto", "tel"];

    private LinkTarget(string value) => Value = value;

    /// <summary>Gets the address exactly as supplied.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Creates a link target from text.</summary>
    /// <param name="value">The candidate address.</param>
    /// <returns>The target, or an input-independent validation error.</returns>
    public static Result<LinkTarget> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Invalid("A link address is required.");
        }

        if (value.Any(char.IsControl) || value.Contains('\\', StringComparison.Ordinal) || char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            return Invalid("A link address must not contain control characters, backslashes or leading or trailing white space.");
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return Invalid("A protocol-relative link address is not allowed.");
        }

        var schemeEnd = value.IndexOfAny([':', '/', '?', '#']);
        if (schemeEnd > 0 && value[schemeEnd] == ':' && !Schemes.Contains(value[..schemeEnd], StringComparer.OrdinalIgnoreCase))
        {
            return Invalid("Only relative addresses and the http, https, mailto and tel schemes are allowed.");
        }

        return Result.Ok(new LinkTarget(value));
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

    /// <summary>Converts text to a target with <see cref="FromString" />, so markup can write <c>Href="@("/tours")"</c>.</summary>
    /// <param name="value">The address.</param>
    /// <exception cref="ArgumentException"><paramref name="value" /> is not a safe link address.</exception>
    public static implicit operator LinkTarget(string value) => FromString(value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static Result<LinkTarget> Invalid(string message) =>
        Result.Invalid<LinkTarget>(detail: message, field: "href", message: message);
}
