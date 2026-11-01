using PandaBear.InputNormalization;

namespace PandaBear.ObjectStorage;

/// <summary>A validated relative slash-delimited object identity, compared by its exact text.</summary>
public readonly record struct ObjectStorageKey
{
    private readonly string? value;

    private ObjectStorageKey(string value) => this.value = value;

    /// <summary>Gets the unchanged key text.</summary>
    /// <exception cref="InvalidOperationException">The key has not been initialized through <see cref="From" />.</exception>
    public string Value => value ?? throw new InvalidOperationException("Object storage keys must be created through ObjectStorageKey.From.");

    /// <summary>Creates a relative key without trimming, changing case, or decoding percent escapes.</summary>
    /// <param name="value">The exact object identity.</param>
    /// <param name="maxLength">The positive provider or application length limit.</param>
    /// <returns>The validated key.</returns>
    /// <exception cref="ArgumentException">The key is not a relative slash-delimited path without dot segments, or exceeds the limit.</exception>
    public static ObjectStorageKey From(string value, int maxLength = int.MaxValue) =>
        TryFrom(value, out var key, maxLength) ? key : throw new ArgumentException("Object key must be a relative slash-delimited path without dot segments and within the length limit.", nameof(value));

    /// <summary>Attempts to validate an exact relative object identity.</summary>
    /// <param name="value">The candidate identity.</param>
    /// <param name="key">The validated key, or the uninitialized default on failure.</param>
    /// <param name="maxLength">The positive provider or application length limit.</param>
    /// <returns>Whether the candidate is valid.</returns>
    public static bool TryFrom(string? value, out ObjectStorageKey key, int maxLength = int.MaxValue)
    {
        key = default;
        if (!ObjectStorageKeyValidator.IsValidRelativeKey(value, maxLength))
        {
            return false;
        }

        key = new ObjectStorageKey(value!);
        return true;
    }

    /// <summary>Escapes each path segment independently for use beneath a provider-owned public URI.</summary>
    /// <returns>The escaped relative path, retaining slash separators.</returns>
    /// <exception cref="InvalidOperationException">The key has not been initialized.</exception>
    public string ToEscapedPath() => string.Join('/', Value.Split('/').Select(Uri.EscapeDataString));

    /// <inheritdoc />
    public override string ToString() => Value;
}
