using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A canonical IBAN matching SWIFT Registry release 103 and MOD97-10; not proof of ownership or an active account.</summary>
public sealed class Iban : ValueObject
{
    private Iban(string value) => Value = value;

    /// <summary>Gets the sensitive electronic-format IBAN. Do not log or expose it without authorization.</summary>
    public string Value { get; }

    /// <summary>Creates an IBAN from uppercase ASCII electronic format without spaces.</summary>
    /// <param name="value">The canonical candidate.</param>
    /// <returns>A structurally valid IBAN or an input-independent validation error.</returns>
    public static Result<Iban> Create(string? value)
    {
        if (value is null || value.Length is < 5 or > 34)
        {
            return Invalid();
        }

        var pattern = GetPattern(value[..2]);
        if (value.Length != pattern.Length + 2 || pattern.Length == 0)
        {
            return Invalid();
        }

        for (var index = 0; index < pattern.Length; index++)
        {
            var character = value[index + 2];
            var valid = pattern[index] switch
            {
                'n' => char.IsAsciiDigit(character),
                'a' => character is >= 'A' and <= 'Z',
                'c' => char.IsAsciiDigit(character) || character is >= 'A' and <= 'Z',
                _ => false
            };
            if (!valid)
            {
                return Invalid();
            }
        }

        var checkDigits = (value[2] - '0') * 10 + value[3] - '0';
        if (checkDigits is < 2 or > 98)
        {
            return Invalid();
        }

        var remainder = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[(index + 4) % value.Length];
            remainder = char.IsAsciiDigit(character)
                ? (remainder * 10 + character - '0') % 97
                : (remainder * 100 + character - 'A' + 10) % 97;
        }

        return remainder == 1 ? Result.Ok(new Iban(value)) : Invalid();
    }

    /// <inheritdoc />
    public override string ToString() => "Iban [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static Result<Iban> Invalid() =>
        Result.Invalid<Iban>(detail: "Invalid canonical IBAN.", field: "iban", message: "Invalid canonical IBAN.");

    // Country layouts, including check digits: SWIFT IBAN Registry release 103, September 2026.
    // n = ASCII digit; a = uppercase ASCII letter; c = either.
    private static string GetPattern(string countryCode) => countryCode switch
    {
        "AD" => "nnnnnnnnnncccccccccccc",
        "AE" => "nnnnnnnnnnnnnnnnnnnnn",
        "AL" => "nnnnnnnnnncccccccccccccccc",
        "AT" => "nnnnnnnnnnnnnnnnnn",
        "AZ" => "nnaaaacccccccccccccccccccc",
        "BA" => "nnnnnnnnnnnnnnnnnn",
        "BE" => "nnnnnnnnnnnnnn",
        "BG" => "nnaaaannnnnncccccccc",
        "BH" => "nnaaaacccccccccccccc",
        "BI" => "nnnnnnnnnnnnnnnnnnnnnnnnn",
        "BR" => "nnccccccccnnnnnnnnnnnnnnncc",
        "BY" => "nnccccnnnncccccccccccccccc",
        "CH" => "nnnnnnncccccccccccc",
        "CR" => "nnnnnnnnnnnnnnnnnnnn",
        "CY" => "nnnnnnnnnncccccccccccccccc",
        "CZ" => "nnnnnnnnnnnnnnnnnnnnnn",
        "DE" => "nnnnnnnnnnnnnnnnnnnn",
        "DJ" => "nnnnnnnnnnnnnnnnnnnnnnnnn",
        "DK" => "nnnnnnnnnnnnnnnn",
        "DO" => "nnccccnnnnnnnnnnnnnnnnnnnn",
        "EE" => "nnnnnnnnnnnnnnnnnn",
        "EG" => "nnnnnnnnnnnnnnnnnnnnnnnnnnn",
        "ES" => "nnnnnnnnnnnnnnnnnnnnnn",
        "FI" => "nnnnnnnnnnnnnnnn",
        "FK" => "nnaannnnnnnnnnnn",
        "FO" => "nnnnnnnnnnnnnnnn",
        "FR" => "nnnnnnnnnnnncccccccccccnn",
        "GB" => "nnaaaannnnnnnnnnnnnn",
        "GE" => "nnaannnnnnnnnnnnnnnn",
        "GI" => "nnaaaaccccccccccccccc",
        "GL" => "nnnnnnnnnnnnnnnn",
        "GR" => "nnnnnnnnncccccccccccccccc",
        "GT" => "nncccccccccccccccccccccccc",
        "HN" => "nnaaaannnnnnnnnnnnnnnnnnnn",
        "HR" => "nnnnnnnnnnnnnnnnnnn",
        "HU" => "nnnnnnnnnnnnnnnnnnnnnnnnnn",
        "IE" => "nnaaaannnnnnnnnnnnnn",
        "IL" => "nnnnnnnnnnnnnnnnnnnnn",
        "IQ" => "nnaaaannnnnnnnnnnnnnn",
        "IS" => "nnnnnnnnnnnnnnnnnnnnnnnn",
        "IT" => "nnannnnnnnnnncccccccccccc",
        "JO" => "nnaaaannnncccccccccccccccccc",
        "KW" => "nnaaaacccccccccccccccccccccc",
        "KZ" => "nnnnnccccccccccccc",
        "LB" => "nnnnnncccccccccccccccccccc",
        "LC" => "nnaaaacccccccccccccccccccccccc",
        "LI" => "nnnnnnncccccccccccc",
        "LT" => "nnnnnnnnnnnnnnnnnn",
        "LU" => "nnnnnccccccccccccc",
        "LV" => "nnaaaaccccccccccccc",
        "LY" => "nnnnnnnnnnnnnnnnnnnnnnn",
        "MC" => "nnnnnnnnnnnncccccccccccnn",
        "MD" => "nncccccccccccccccccccc",
        "ME" => "nnnnnnnnnnnnnnnnnnnn",
        "MK" => "nnnnnccccccccccnn",
        "MN" => "nnnnnnnnnnnnnnnnnn",
        "MR" => "nnnnnnnnnnnnnnnnnnnnnnnnn",
        "MT" => "nnaaaannnnncccccccccccccccccc",
        "MU" => "nnaaaannnnnnnnnnnnnnnnnnnaaa",
        "NI" => "nnaaaannnnnnnnnnnnnnnnnnnn",
        "NL" => "nnaaaannnnnnnnnn",
        "NO" => "nnnnnnnnnnnnn",
        "OM" => "nnnnncccccccccccccccc",
        "PK" => "nnaaaacccccccccccccccc",
        "PL" => "nnnnnnnnnnnnnnnnnnnnnnnnnn",
        "PS" => "nnaaaaccccccccccccccccccccc",
        "PT" => "nnnnnnnnnnnnnnnnnnnnnnn",
        "QA" => "nnaaaaccccccccccccccccccccc",
        "RO" => "nnaaaacccccccccccccccc",
        "RS" => "nnnnnnnnnnnnnnnnnnnn",
        "RU" => "nnnnnnnnnnnnnnnnccccccccccccccc",
        "SA" => "nnnncccccccccccccccccc",
        "SC" => "nnaaaannnnnnnnnnnnnnnnnnnnaaa",
        "SD" => "nnnnnnnnnnnnnnnn",
        "SE" => "nnnnnnnnnnnnnnnnnnnnnn",
        "SI" => "nnnnnnnnnnnnnnnnn",
        "SK" => "nnnnnnnnnnnnnnnnnnnnnn",
        "SM" => "nnannnnnnnnnncccccccccccc",
        "SO" => "nnnnnnnnnnnnnnnnnnnnn",
        "ST" => "nnnnnnnnnnnnnnnnnnnnnnn",
        "SV" => "nnaaaannnnnnnnnnnnnnnnnnnn",
        "TL" => "nnnnnnnnnnnnnnnnnnnnn",
        "TN" => "nnnnnnnnnnnnnnnnnnnnnn",
        "TR" => "nnnnnnnncccccccccccccccc",
        "UA" => "nnnnnnnnccccccccccccccccccc",
        "VA" => "nnnnnnnnnnnnnnnnnnnn",
        "VG" => "nnaaaannnnnnnnnnnnnnnn",
        "XK" => "nnnnnnnnnnnnnnnnnn",
        "YE" => "nnaaaannnncccccccccccccccccc",
        _ => string.Empty
    };
}
