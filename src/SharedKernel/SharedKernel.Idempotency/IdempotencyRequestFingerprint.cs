using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PandaBear.Idempotency;

/// <summary>Creates deterministic fingerprints from ordered request components without retaining their values.</summary>
public static class IdempotencyRequestFingerprint
{
    /// <summary>Hashes typed JSON after sorting object properties and normalizing decimal-representable numbers.</summary>
    /// <typeparam name="T">The payload contract.</typeparam>
    /// <param name="input">The immutable input snapshot to serialize, including null when supported by its contract.</param>
    /// <param name="type">Source-generated JSON metadata for the payload contract.</param>
    /// <param name="components">Opaque context components prepended to the canonical JSON using the existing length framing.</param>
    /// <returns>The uppercase SHA-256 fingerprint, with no retained payload.</returns>
    /// <remarks>
    /// Array order and string values remain significant. Callers own domain-specific selection ordering and actor binding.
    /// This is deterministic request normalization, not an implementation of RFC 8785.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="type"/>, <paramref name="components"/>, or a context component is null.</exception>
    public static string CreateJson<T>(T input, JsonTypeInfo<T> type, params string[] components)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(components);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(input, type);
        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            using var document = JsonDocument.Parse(bytes);
            using (var writer = new Utf8JsonWriter(buffer)) { WriteNormalized(document.RootElement, writer); }
            return Create([.. components, Encoding.UTF8.GetString(buffer.WrittenSpan)]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            buffer.Clear();
        }
    }

    private static void WriteNormalized(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); WriteNormalized(property.Value, writer); }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var element in value.EnumerateArray()) { WriteNormalized(element, writer); }
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number when value.TryGetDecimal(out var number):
                writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture));
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }

    /// <summary>Hashes ordered, length-framed string components with SHA-256.</summary>
    /// <param name="components">The non-null components, in their contract-defined order. Empty strings are allowed.</param>
    /// <returns>The uppercase hexadecimal SHA-256 hash of the UTF-8 encoded framing.</returns>
    /// <remarks>
    /// Each component is framed as its invariant decimal UTF-16 length, a colon, and its unchanged value.
    /// Frames are separated by a colon. No trimming or Unicode normalization is performed.
    /// An empty component list hashes the empty string. Hashing is not encryption and does not conceal low-entropy inputs.
    /// </remarks>
    public static string Create(params string[] components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var framed = new StringBuilder();
        foreach (var component in components)
        {
            ArgumentNullException.ThrowIfNull(component);
            if (framed.Length > 0)
            {
                framed.Append(':');
            }

            framed.Append(component.Length.ToString(CultureInfo.InvariantCulture));
            framed.Append(':');
            framed.Append(component);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(framed.ToString())));
    }
}
