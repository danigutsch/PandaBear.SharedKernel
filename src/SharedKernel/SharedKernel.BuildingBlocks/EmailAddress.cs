using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A structurally valid email address, stored in lower case; not proof the mailbox exists or is owned by the user.</summary>
public sealed class EmailAddress : ValueObject
{
    /// <summary>The maximum total length of an address (RFC 5321 path limit).</summary>
    public const int MaxLength = 254;

    private const int MaxLocalPartLength = 64;

    private EmailAddress(string value) => Value = value;

    /// <summary>Gets the lower-case address. Personal data: do not log it without a reason.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Creates an address from text. Surrounding whitespace is removed and the result is lower-cased.</summary>
    /// <param name="value">The candidate address.</param>
    /// <returns>The address or an input-independent validation error.</returns>
    public static Result<EmailAddress> Create(string? value)
    {
        var sanitized = StringSanitizer.Sanitize(value) ?? string.Empty;
        if (sanitized.Length == 0)
        {
            return Invalid("Email is required.");
        }

        if (sanitized.Length > MaxLength)
        {
            return Invalid($"Email cannot exceed {MaxLength} characters.");
        }

        var at = sanitized.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != sanitized.LastIndexOf('@') || at > MaxLocalPartLength)
        {
            return Invalid("Email format is invalid.");
        }

        var domain = sanitized[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        if (sanitized.Contains(' ', StringComparison.Ordinal) || dot < 1 || dot == domain.Length - 1 || domain.StartsWith('.') || domain.Contains("..", StringComparison.Ordinal))
        {
            return Invalid("Email format is invalid.");
        }

#pragma warning disable CA1308 // Lower case is the canonical form for case-insensitive email comparison, not a security normalization.
        return Result.Ok(new EmailAddress(sanitized.ToLowerInvariant()));
#pragma warning restore CA1308
    }

    /// <inheritdoc />
    public override string ToString() => "EmailAddress [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static Result<EmailAddress> Invalid(string message) =>
        Result.Invalid<EmailAddress>(detail: message, field: "email", message: message);
}
