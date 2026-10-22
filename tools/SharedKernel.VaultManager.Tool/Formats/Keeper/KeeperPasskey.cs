using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>Converts Keeper passkey fields to Bitwarden passkeys.</summary>
internal static class KeeperPasskey
{
    /// <summary>Converts a Keeper <c>passkey</c> value.</summary>
    /// <param name="value">Object with <c>privateKey</c> (EC P-256 JWK), <c>credentialId</c>, <c>signCount</c>, <c>userId</c>, <c>relyingParty</c>, <c>username</c>, and <c>createdDate</c> in milliseconds.</param>
    /// <returns>The passkey, or <see langword="null"/> when the value is incomplete or the key is not a valid P-256 key.</returns>
    public static Fido2Credential? TryConvert(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object
            || Text(value, "relyingParty") is not { Length: > 0 } relyingParty
            || Text(value, "credentialId") is not { Length: > 0 } credentialId
            || !value.TryGetProperty("privateKey", out JsonElement jwk)
            || jwk.ValueKind != JsonValueKind.Object
            || DecodeBase64Url(credentialId) is not { Length: > 0 } credentialBytes)
        {
            return null;
        }

        string? keyValue = ConvertKey(jwk);
        if (keyValue is null)
        {
            return null;
        }

        DateTimeOffset created = value.TryGetProperty("createdDate", out JsonElement date) && date.TryGetInt64(out long milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : default;
        string counter = value.TryGetProperty("signCount", out JsonElement count) && count.TryGetInt64(out long signCount)
            ? signCount.ToString(CultureInfo.InvariantCulture)
            : "0";

        return new Fido2Credential
        {
            CredentialId = "b64." + EncodeBase64Url(credentialBytes),
            KeyValue = keyValue,
            RpId = relyingParty,
            UserHandle = Text(value, "userId"),
            UserName = Text(value, "username"),
            Counter = counter,
            Discoverable = "true",
            CreationDate = created,
        };
    }

    private static string? ConvertKey(JsonElement jwk)
    {
        if (Text(jwk, "kty") != "EC" || Text(jwk, "crv") != "P-256")
        {
            return null;
        }

        byte[]? d = DecodeBase64Url(Text(jwk, "d"));
        byte[]? x = DecodeBase64Url(Text(jwk, "x"));
        byte[]? y = DecodeBase64Url(Text(jwk, "y"));
        if (d is not { Length: 32 } || x is not { Length: 32 } || y is not { Length: 32 })
        {
            if (d is not null)
            {
                CryptographicOperations.ZeroMemory(d);
            }

            return null;
        }

        byte[]? pkcs8 = null;
        try
        {
            using var key = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = d,
                Q = new ECPoint { X = x, Y = y },
            });
            pkcs8 = key.ExportPkcs8PrivateKey();
            return EncodeBase64Url(pkcs8);
        }
        catch (CryptographicException)
        {
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(d);
            if (pkcs8 is not null)
            {
                CryptographicOperations.ZeroMemory(pkcs8);
            }
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static byte[]? DecodeBase64Url(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        string base64 = value.TrimEnd('=').Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        byte[] buffer = new byte[base64.Length * 3 / 4];
        if (!Convert.TryFromBase64String(base64, buffer, out int written))
        {
            return null;
        }

        byte[] result = buffer[..written];
        CryptographicOperations.ZeroMemory(buffer);
        return result;
    }

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
