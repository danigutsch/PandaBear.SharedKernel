namespace PandaBear.ObjectStorage;

/// <summary>An exact object-storage listing prefix; an empty prefix selects every key.</summary>
public readonly record struct ObjectStoragePrefix
{
    private readonly string? value;

    private ObjectStoragePrefix(string value) => this.value = value;

    /// <summary>Gets the unchanged prefix text. The default value is the empty prefix.</summary>
    public string Value => value ?? string.Empty;

    /// <summary>Creates a prefix, preserving partial segments and trailing slash semantics.</summary>
    /// <param name="value">An empty prefix or a relative key optionally followed by slashes.</param>
    /// <param name="maxLength">The positive length limit for the prefix without trailing slashes.</param>
    /// <returns>The validated prefix.</returns>
    /// <exception cref="ArgumentException">The prefix is invalid or exceeds the limit.</exception>
    public static ObjectStoragePrefix From(string value, int maxLength = int.MaxValue) =>
        TryFrom(value, out var prefix, maxLength) ? prefix : throw new ArgumentException("Object prefix must be empty or a relative slash-delimited path without dot segments and within the length limit.", nameof(value));

    /// <summary>Attempts to validate an exact listing prefix.</summary>
    /// <param name="value">The candidate prefix.</param>
    /// <param name="prefix">The validated prefix, or the empty prefix on failure.</param>
    /// <param name="maxLength">The positive length limit for the prefix without trailing slashes.</param>
    /// <returns>Whether the candidate is valid.</returns>
    public static bool TryFrom(string? value, out ObjectStoragePrefix prefix, int maxLength = int.MaxValue)
    {
        prefix = default;
        if (value is null || maxLength <= 0
            || (value.Length > 0 && !ObjectStorageKey.TryFrom(value.TrimEnd('/'), out _, maxLength)))
        {
            return false;
        }

        // The empty prefix has one representation, including default(ObjectStoragePrefix).
        prefix = value.Length == 0 ? default : new ObjectStoragePrefix(value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
