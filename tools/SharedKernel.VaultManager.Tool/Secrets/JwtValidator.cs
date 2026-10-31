using System.Text.Json;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Checks that a JSON web token header decodes to a JSON object with an <c>alg</c> member.</summary>
internal static class JwtValidator
{
    /// <summary>Whether the first segment decodes to a JSON object carrying <c>alg</c>.</summary>
    /// <param name="token">Candidate token.</param>
    /// <returns><see langword="true"/> when the header is well formed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    public static bool HasAlgorithmHeader(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        int dot = token.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0 || !SecretText.TryDecodeBase64Url(token.AsSpan(0, dot), out byte[]? header))
        {
            return false;
        }

        try
        {
            var reader = new Utf8JsonReader(header);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("alg"u8))
                {
                    return reader.Read() && reader.TokenType == JsonTokenType.String;
                }

                reader.Skip();
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
