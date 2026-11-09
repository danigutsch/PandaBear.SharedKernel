using System.Buffers.Binary;
using System.Text;
using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// A public age recipient. Accepted forms: Bech32 recipients whose human-readable part is on the allow-list
/// (<c>age</c>, <c>age1pq</c>, <c>age1tag</c>, <c>age1tagpq</c>, <c>age1yubikey</c>, <c>age1tpm</c>, <c>age1se</c>,
/// <c>age1fido2-hmac</c>), and SSH recipient lines (<c>ssh-ed25519</c>, <c>ssh-rsa</c>) without a comment. Bech32 is
/// verified as BIP-173 Bech32 (not Bech32m) without a length limit, as age does. A Bech32 recipient is stored in lowercase,
/// so an uppercase spelling equals the lowercase one. A private identity is never accepted here.
/// </summary>
public sealed class AgeRecipient : ValueObject
{
    /// <summary>The longest accepted recipient text.</summary>
    public const int MaxLength = 4096;

    private const int X25519KeyLength = 32;

    private static readonly Dictionary<string, AgeRecipientKind> Prefixes = new(StringComparer.Ordinal)
    {
        ["age"] = AgeRecipientKind.X25519,
        ["age1pq"] = AgeRecipientKind.Hybrid,
        ["age1tag"] = AgeRecipientKind.Tag,
        ["age1tagpq"] = AgeRecipientKind.Tag,
        ["age1yubikey"] = AgeRecipientKind.Plugin,
        ["age1tpm"] = AgeRecipientKind.Plugin,
        ["age1se"] = AgeRecipientKind.Plugin,
        ["age1fido2-hmac"] = AgeRecipientKind.Plugin,
    };

    private AgeRecipient(string value, AgeRecipientKind kind)
    {
        Value = value;
        Kind = kind;
    }

    /// <summary>Gets the recipient text, in lowercase for Bech32 recipients.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Gets the kind of recipient.</summary>
    public AgeRecipientKind Kind
    {
        get;
    }

    /// <summary>Creates a recipient from <paramref name="value"/>.</summary>
    /// <param name="value">The candidate recipient.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The recipient or a validation error that never echoes the input.</returns>
    public static Result<AgeRecipient> Create(string? value, string field = "recipient")
    {
        var problem = Validate(value, out var normalized, out var kind);
        return problem is null ? Result.Ok(new AgeRecipient(normalized, kind)) : Result.Invalid<AgeRecipient>(problem, field, problem);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static string? Validate(string? value, out string normalized, out AgeRecipientKind kind)
    {
        normalized = string.Empty;
        kind = AgeRecipientKind.X25519;
        if (string.IsNullOrEmpty(value))
        {
            return "Recipient is required.";
        }

        if (value.Length > MaxLength)
        {
            return $"Recipient cannot exceed {MaxLength} characters.";
        }

        if (value.Contains("AGE-SECRET-KEY-", StringComparison.OrdinalIgnoreCase) || value.Contains("AGE-PLUGIN-", StringComparison.OrdinalIgnoreCase))
        {
            return "Recipient must be a public recipient; this text looks like a private age identity.";
        }

        if (value.StartsWith("ssh-", StringComparison.Ordinal))
        {
            return ValidateSsh(value, out normalized, out kind);
        }

        if (!Bech32.TryDecode(value, maxLength: null, out var hrp, out var data))
        {
            return "Recipient must be a valid Bech32 age recipient or an ssh-ed25519 or ssh-rsa line.";
        }

        if (!Prefixes.TryGetValue(hrp, out kind))
        {
            return "Recipient has a human-readable part that is not on the allow-list.";
        }

        if (data.Length == 0)
        {
            return "Recipient has no data.";
        }

        if (kind == AgeRecipientKind.X25519 && !(Bech32.TryToBytes(data, out var bytes) && bytes.Length == X25519KeyLength))
        {
            return "An age recipient must carry a 32-byte X25519 public key.";
        }

        normalized = value.Any(char.IsAsciiLetterUpper) ? LowerAscii(value) : value;
        return null;
    }

    private static string? ValidateSsh(string value, out string normalized, out AgeRecipientKind kind)
    {
        normalized = string.Empty;
        kind = AgeRecipientKind.Ssh;
        var parts = value.Split(' ');
        if (parts.Length != 2 || parts[1].Length == 0 || parts[0] is not ("ssh-ed25519" or "ssh-rsa"))
        {
            return "An SSH recipient must be 'ssh-ed25519 <key>' or 'ssh-rsa <key>' without a comment.";
        }

        var blob = new byte[parts[1].Length];
        if (!Convert.TryFromBase64String(parts[1], blob, out var written) || written < 4)
        {
            return "The SSH recipient key is not valid base64.";
        }

        var typeLength = BinaryPrimitives.ReadUInt32BigEndian(blob);
        var expected = Encoding.ASCII.GetBytes(parts[0]);
        if (typeLength != expected.Length || written < 4 + expected.Length || !blob.AsSpan(4, expected.Length).SequenceEqual(expected))
        {
            return "The SSH recipient key does not match its key type.";
        }

        normalized = value;
        return null;
    }

    private static string LowerAscii(string value) =>
        string.Create(value.Length, value, static (span, source) =>
        {
            for (var index = 0; index < span.Length; index++)
            {
                span[index] = char.IsAsciiLetterUpper(source[index]) ? (char)(source[index] | 0x20) : source[index];
            }
        });
}
