using System.Buffers;
using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>A typed canonical Pix key with validated format; not proof of DICT registration or recipient ownership.</summary>
public sealed class PixKey : ValueObject
{
    private static readonly SearchValues<char> EmailLocalCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789.!#$'*+/=?^_`{|}~-");

    private static readonly SearchValues<char> DomainLabelCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789-");

    private PixKey(PixKeyType type, string value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>Gets the explicitly selected key type.</summary>
    public PixKeyType Type { get; }

    /// <summary>Gets the sensitive key. Do not log or expose it without authorization.</summary>
    public string Value { get; }

    /// <summary>Creates a typed key using BCB DICT API 2.12.1 formats and taxpayer check digits.</summary>
    /// <param name="type">The selected format; never inferred from the candidate.</param>
    /// <param name="value">The canonical candidate, at most 77 characters.</param>
    /// <returns>A validated key or an input-independent validation error.</returns>
    public static Result<PixKey> Create(PixKeyType type, string? value)
    {
        if (value is null || value.Length is 0 or > 77)
        {
            return Invalid();
        }

        var valid = type switch
        {
            PixKeyType.Cpf => Cpf.Create(value).IsSuccess,
            PixKeyType.Cnpj => Cnpj.Create(value).IsSuccess,
            PixKeyType.Phone => value.Length is >= 3 and <= 16
                && value[0] == '+' && value[1] is >= '1' and <= '9'
                && value.AsSpan(2).IndexOfAnyExceptInRange('0', '9') < 0,
            PixKeyType.Email => IsEmail(value),
            PixKeyType.Evp => value.Length == 36
                && !value.Any(static character => !char.IsAsciiHexDigitLower(character) && character != '-')
                && Guid.TryParseExact(value, "D", out _),
            _ => false
        };

        return valid ? Result.Ok(new PixKey(type, value)) : Invalid();
    }

    /// <inheritdoc />
    public override string ToString() => "PixKey [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Value;
    }

    private static Result<PixKey> Invalid() =>
        Result.Invalid<PixKey>(detail: "Pix key is not valid.", field: "pixKey", message: "Pix key is not valid.");

    // Parses the DICT email format without a regular expression, so a busy host cannot turn a match timeout into an exception.
    private static bool IsEmail(ReadOnlySpan<char> value)
    {
        var at = value.IndexOf('@');
        if (at <= 0 || value[..at].ContainsAnyExcept(EmailLocalCharacters))
        {
            return false;
        }

        var domain = value[(at + 1)..];
        foreach (var range in domain.Split('.'))
        {
            var label = domain[range];
            if (label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-' || label.ContainsAnyExcept(DomainLabelCharacters))
            {
                return false;
            }
        }

        return true;
    }
}
