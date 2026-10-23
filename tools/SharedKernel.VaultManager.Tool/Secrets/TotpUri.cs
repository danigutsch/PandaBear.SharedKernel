using System.Globalization;
using System.Text;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Normalizes authenticator secrets to the forms Bitwarden accepts in <c>login.totp</c>, and decodes Google
/// Authenticator <c>otpauth-migration://</c> exports.
/// </summary>
/// <remarks>
/// Migration payload field numbers follow the public <c>migration.proto</c> description in dim13/otpauth
/// (commit 027a7cc0957463b4a5eaefb26837e8e26cfd5f67): <c>Payload.otp_parameters = 1</c>; <c>OtpParameters</c> has
/// secret 1, name 2, issuer 3, algorithm 4 (1 SHA1, 2 SHA256, 3 SHA512, 4 MD5), digits 5 (1 six, 2 eight),
/// type 6 (1 HOTP, 2 TOTP), and counter 7.
/// </remarks>
internal static class TotpUri
{
    private const string OtpauthPrefix = "otpauth://";
    private const string TotpPrefix = "otpauth://totp/";
    private const string HotpPrefix = "otpauth://hotp/";
    private const string SteamPrefix = "steam://";
    private const string MigrationPrefix = "otpauth-migration://";

    /// <summary>Whether a value is an HOTP URI, which Bitwarden does not support.</summary>
    /// <param name="value">Value.</param>
    /// <returns><see langword="true"/> for <c>otpauth://hotp/</c>.</returns>
    public static bool IsHotp(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.TrimStart().StartsWith(HotpPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Normalizes an <c>otpauth://totp</c> URI, a <c>steam://</c> secret, or a plain base32 secret.</summary>
    /// <param name="value">Raw value.</param>
    /// <param name="defaultIssuer">Issuer used when the value names none, for example the item name.</param>
    /// <param name="defaultAccount">Account used when the value names none, for example the login user name.</param>
    /// <param name="uri">Normalized URI.</param>
    /// <returns><see langword="true"/> when the value holds a usable TOTP secret.</returns>
    public static bool TryNormalize(string value, string? defaultIssuer, string? defaultAccount, out string? uri)
    {
        ArgumentNullException.ThrowIfNull(value);
        uri = null;
        string trimmed = value.Trim();
        if (trimmed.StartsWith(OtpauthPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.StartsWith(TotpPrefix, StringComparison.OrdinalIgnoreCase) && TryNormalizeOtpauth(trimmed, defaultIssuer, defaultAccount, out uri);
        }

        if (trimmed.StartsWith(SteamPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string steamSecret = Base32.Normalize(trimmed[SteamPrefix.Length..]);
            if (!Base32.IsTotpSecret(steamSecret))
            {
                return false;
            }

            uri = SteamPrefix + steamSecret;
            return true;
        }

        if (trimmed.Any(character => !(char.IsAsciiLetter(character) || character is (>= '2' and <= '7') or ' ' or '-' or '=')))
        {
            return false;
        }

        string secret = Base32.Normalize(trimmed);
        if (!Base32.IsTotpSecret(secret))
        {
            return false;
        }

        uri = Build(defaultIssuer, defaultAccount, secret, "SHA1", 6, 30);
        return true;
    }

    /// <summary>Decodes a Google Authenticator migration URI.</summary>
    /// <param name="value">The <c>otpauth-migration://offline?data=</c> URI.</param>
    /// <returns>Decoded accounts, or an empty list when the payload is malformed.</returns>
    public static IReadOnlyList<MigratedOtp> DecodeMigration(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string trimmed = value.Trim();
        int data = trimmed.IndexOf("data=", StringComparison.Ordinal);
        if (!trimmed.StartsWith(MigrationPrefix, StringComparison.OrdinalIgnoreCase) || data < 0)
        {
            return [];
        }

        string encoded = trimmed[(data + 5)..];
        int ampersand = encoded.IndexOf('&', StringComparison.Ordinal);
        if (ampersand >= 0)
        {
            encoded = encoded[..ampersand];
        }

        byte[]? payload;
        try
        {
            encoded = Uri.UnescapeDataString(encoded).Replace('-', '+').Replace('_', '/').Replace(' ', '+');
        }
        catch (UriFormatException)
        {
            return [];
        }

        encoded = encoded.PadRight(encoded.Length + ((4 - (encoded.Length % 4)) % 4), '=');
        if (!SecretText.TryDecodeBase64(encoded, out payload) || payload is null)
        {
            return [];
        }

        var entries = new List<MigratedOtp>();
        var reader = new ProtobufReader(payload);
        while (!reader.AtEnd)
        {
            if (!reader.TryReadTag(out int field, out int wireType))
            {
                return [];
            }

            if (field == 1 && wireType == 2)
            {
                if (!reader.TryReadLengthDelimited(out ReadOnlyMemory<byte> message) || !TryDecodeParameters(message, out MigratedOtp? entry))
                {
                    return [];
                }

                entries.Add(entry!);
            }
            else if (!reader.TrySkip(wireType))
            {
                return [];
            }
        }

        return entries;
    }

    private static bool TryDecodeParameters(ReadOnlyMemory<byte> message, out MigratedOtp? entry)
    {
        entry = null;
        byte[] secret = [];
        string name = string.Empty;
        string issuer = string.Empty;
        ulong algorithm = 0;
        ulong digits = 0;
        ulong type = 0;
        var reader = new ProtobufReader(message);
        while (!reader.AtEnd)
        {
            if (!reader.TryReadTag(out int field, out int wireType))
            {
                return false;
            }

            bool ok = (field, wireType) switch
            {
                (1, 2) => reader.TryReadBytes(out secret),
                (2, 2) => reader.TryReadString(out name),
                (3, 2) => reader.TryReadString(out issuer),
                (4, 0) => reader.TryReadVarint(out algorithm),
                (5, 0) => reader.TryReadVarint(out digits),
                (6, 0) => reader.TryReadVarint(out type),
                _ => reader.TrySkip(wireType),
            };
            if (!ok)
            {
                return false;
            }
        }

        string account = name;
        int colon = name.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && (issuer.Length == 0 || string.Equals(name[..colon], issuer, StringComparison.Ordinal)))
        {
            issuer = issuer.Length == 0 ? name[..colon] : issuer;
            account = name[(colon + 1)..].TrimStart();
        }

        string? algorithmName = algorithm switch
        {
            0 or 1 => "SHA1",
            2 => "SHA256",
            3 => "SHA512",
            _ => null,
        };
        string? problem = type == 1 ? "HOTP is not supported by Bitwarden"
            : algorithmName is null ? "MD5 or unknown algorithm is not supported by Bitwarden"
            : secret.Length * 8 < Base32.MinimumTotpBits ? "secret shorter than 80 bits"
            : null;
        string? uri = problem is null
            ? Build(issuer.Length == 0 ? null : issuer, account.Length == 0 ? null : account, Base32.Encode(secret), algorithmName!, digits == 2 ? 8 : 6, 30)
            : null;
        entry = new MigratedOtp(account, issuer, problem is null, uri, problem);
        return true;
    }

    private static bool TryNormalizeOtpauth(string value, string? defaultIssuer, string? defaultAccount, out string? uri)
    {
        uri = null;
        string rest = value[TotpPrefix.Length..];
        int question = rest.IndexOf('?', StringComparison.Ordinal);
        string label = question < 0 ? rest : rest[..question];
        string query = question < 0 ? string.Empty : rest[(question + 1)..];
        string? labelIssuer = null;
        string? account;
        try
        {
            label = Uri.UnescapeDataString(label);
            int colon = label.IndexOf(':', StringComparison.Ordinal);
            if (colon >= 0)
            {
                labelIssuer = label[..colon].Trim();
                account = label[(colon + 1)..].Trim();
            }
            else
            {
                account = label.Trim();
            }
        }
        catch (UriFormatException)
        {
            return false;
        }

        string? secret = null;
        string? issuer = null;
        string algorithm = "SHA1";
        int digits = 6;
        int period = 30;
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            string key = (equals < 0 ? pair : pair[..equals]).ToUpperInvariant();
            string raw = equals < 0 ? string.Empty : pair[(equals + 1)..];
            string parameter;
            try
            {
                parameter = Uri.UnescapeDataString(raw.Replace('+', ' '));
            }
            catch (UriFormatException)
            {
                return false;
            }

            switch (key)
            {
                case "SECRET":
                    secret = Base32.Normalize(parameter);
                    break;
                case "ISSUER":
                    issuer = parameter.Trim();
                    break;
                case "ALGORITHM":
                    string upper = parameter.Trim().ToUpperInvariant();
                    if (upper is not ("SHA1" or "SHA256" or "SHA512"))
                    {
                        return false;
                    }

                    algorithm = upper;
                    break;
                case "DIGITS" when !int.TryParse(parameter, NumberStyles.None, CultureInfo.InvariantCulture, out digits) || digits is < 1 or > 10:
                case "PERIOD" when !int.TryParse(parameter, NumberStyles.None, CultureInfo.InvariantCulture, out period) || period is < 1 or > 86_400:
                    return false;
                default:
                    break;
            }
        }

        if (secret is null || !Base32.IsTotpSecret(secret))
        {
            return false;
        }

        issuer = NullIfEmpty(issuer) ?? NullIfEmpty(labelIssuer) ?? NullIfEmpty(defaultIssuer);
        uri = Build(issuer, NullIfEmpty(account) ?? NullIfEmpty(defaultAccount), secret, algorithm, digits, period);
        return true;
    }

    private static string Build(string? issuer, string? account, string secret, string algorithm, int digits, int period)
    {
        var builder = new StringBuilder(TotpPrefix);
        if (issuer is not null)
        {
            builder.Append(Uri.EscapeDataString(issuer));
            if (account is not null)
            {
                builder.Append(':');
            }
        }

        if (account is not null)
        {
            builder.Append(Uri.EscapeDataString(account));
        }

        builder.Append("?secret=").Append(secret);
        if (issuer is not null)
        {
            builder.Append("&issuer=").Append(Uri.EscapeDataString(issuer));
        }

        if (algorithm != "SHA1")
        {
            builder.Append("&algorithm=").Append(algorithm);
        }

        if (digits != 6)
        {
            builder.Append("&digits=").Append(digits.ToString(CultureInfo.InvariantCulture));
        }

        if (period != 30)
        {
            builder.Append("&period=").Append(period.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
