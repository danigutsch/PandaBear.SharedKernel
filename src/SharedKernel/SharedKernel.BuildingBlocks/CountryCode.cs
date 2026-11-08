using System.Collections.Frozen;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>An officially assigned ISO 3166-1 alpha-2 country code in upper case, for example <c>BR</c>; it names a country, not a person's legal status.</summary>
public sealed class CountryCode : ValueObject
{
    /// <summary>The number of officially assigned ISO 3166-1 alpha-2 codes.</summary>
    public const int AssignedCount = 249;

    // Officially assigned codes (ISO 3166-1 alpha-2; user-assigned and exceptionally reserved codes are not accepted).
    private static readonly FrozenSet<string> Assigned = new[]
    {
        "AD", "AE", "AF", "AG", "AI", "AL", "AM", "AO", "AQ", "AR", "AS", "AT",
        "AU", "AW", "AX", "AZ", "BA", "BB", "BD", "BE", "BF", "BG", "BH", "BI",
        "BJ", "BL", "BM", "BN", "BO", "BQ", "BR", "BS", "BT", "BV", "BW", "BY",
        "BZ", "CA", "CC", "CD", "CF", "CG", "CH", "CI", "CK", "CL", "CM", "CN",
        "CO", "CR", "CU", "CV", "CW", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM",
        "DO", "DZ", "EC", "EE", "EG", "EH", "ER", "ES", "ET", "FI", "FJ", "FK",
        "FM", "FO", "FR", "GA", "GB", "GD", "GE", "GF", "GG", "GH", "GI", "GL",
        "GM", "GN", "GP", "GQ", "GR", "GS", "GT", "GU", "GW", "GY", "HK", "HM",
        "HN", "HR", "HT", "HU", "ID", "IE", "IL", "IM", "IN", "IO", "IQ", "IR",
        "IS", "IT", "JE", "JM", "JO", "JP", "KE", "KG", "KH", "KI", "KM", "KN",
        "KP", "KR", "KW", "KY", "KZ", "LA", "LB", "LC", "LI", "LK", "LR", "LS",
        "LT", "LU", "LV", "LY", "MA", "MC", "MD", "ME", "MF", "MG", "MH", "MK",
        "ML", "MM", "MN", "MO", "MP", "MQ", "MR", "MS", "MT", "MU", "MV", "MW",
        "MX", "MY", "MZ", "NA", "NC", "NE", "NF", "NG", "NI", "NL", "NO", "NP",
        "NR", "NU", "NZ", "OM", "PA", "PE", "PF", "PG", "PH", "PK", "PL", "PM",
        "PN", "PR", "PS", "PT", "PW", "PY", "QA", "RE", "RO", "RS", "RU", "RW",
        "SA", "SB", "SC", "SD", "SE", "SG", "SH", "SI", "SJ", "SK", "SL", "SM",
        "SN", "SO", "SR", "SS", "ST", "SV", "SX", "SY", "SZ", "TC", "TD", "TF",
        "TG", "TH", "TJ", "TK", "TL", "TM", "TN", "TO", "TR", "TT", "TV", "TW",
        "TZ", "UA", "UG", "UM", "US", "UY", "UZ", "VA", "VC", "VE", "VG", "VI",
        "VN", "VU", "WF", "WS", "YE", "YT", "ZA", "ZM", "ZW"
    }.ToFrozenSet(StringComparer.Ordinal);

    private CountryCode(string value) => Value = value;

    /// <summary>Gets the upper-case two-letter code.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Gets the English short name of the country, for example <c>Brazil</c>; it comes from a fixed table, so it does not depend on the host's globalization data or the current culture.</summary>
    public string EnglishName => CountryNames.ByCode.TryGetValue(Value, out var name) ? name : Value;

    /// <summary>Gets every assigned code in ordinal order.</summary>
    public static IReadOnlyList<CountryCode> All { get; } = Assigned.Order(StringComparer.Ordinal).Select(code => new CountryCode(code)).ToArray();

    /// <summary>Gets every assigned code ordered by English name, ignoring case and diacritics (for example <c>Åland Islands</c> sorts among the A names). The order uses an explicit ASCII fold of the table's accented letters and ordinal comparison, so it is identical on every host, also with <c>InvariantGlobalization=true</c>. Ties break by code.</summary>
    public static IReadOnlyList<CountryCode> AllByEnglishName { get; } = All.OrderBy(code => CountryNames.SortKey(code.EnglishName), StringComparer.Ordinal).ThenBy(code => code.Value, StringComparer.Ordinal).ToArray();

    /// <summary>Creates a code from text; case is ignored and surrounding whitespace is removed.</summary>
    /// <param name="value">The candidate code.</param>
    /// <returns>The code or an input-independent validation error.</returns>
    public static Result<CountryCode> Create(string? value)
    {
        var candidate = value?.Trim().ToUpperInvariant();
        return candidate is not null && Assigned.Contains(candidate)
            ? Result.Ok(new CountryCode(candidate))
            : Result.Invalid<CountryCode>(detail: "Country code is not a valid ISO 3166-1 alpha-2 code.", field: "country", message: "Country code is not a valid ISO 3166-1 alpha-2 code.");
    }

    /// <summary>For persistence mappers: rebuilds a value from text that was produced by this type earlier; it performs NO validation beyond null checks and normalization identical to <see cref="Create"/>, so a stored value that a newer rule would reject can still be loaded; never use it for user input. A code outside the assigned set is kept, and its <see cref="EnglishName"/> is the code itself.</summary>
    /// <param name="value">The stored code.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty or blank.</exception>
    public static CountryCode Restore(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new CountryCode(value.Trim().ToUpperInvariant());
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
