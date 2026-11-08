using System.Text.RegularExpressions;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A typed canonical Pix key with validated format; not proof of DICT registration or recipient ownership.</summary>
public sealed partial class PixKey : ValueObject
{
    private PixKey(PixKeyType type, string value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>Gets the explicitly selected key type.</summary>
    public PixKeyType Type
    {
        get;
    }

    /// <summary>Gets the sensitive key. Do not log or expose it without authorization.</summary>
    public string Value
    {
        get;
    }

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
            PixKeyType.Email => EmailPattern().IsMatch(value),
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
        Result.Invalid<PixKey>(detail: "Invalid canonical Pix key.", field: "pixKey", message: "Invalid canonical Pix key.");

    [GeneratedRegex(@"\A[a-z0-9.!#$'*+/=?^_`{|}~-]+@[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)*\z", RegexOptions.CultureInvariant, 100)]
    private static partial Regex EmailPattern();
}
