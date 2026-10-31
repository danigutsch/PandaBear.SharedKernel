namespace PandaBear.InputNormalization;

/// <summary>
/// Validates relative slash-delimited keys with well-formed Unicode and no control characters.
/// </summary>
public static class ObjectStorageKeyValidator
{
    /// <summary>
    /// Determines whether a value is a valid relative object-storage key.
    /// </summary>
    /// <param name="value">The candidate object-storage key.</param>
    /// <param name="maxLength">The maximum accepted key length.</param>
    /// <returns><see langword="true"/> when the key is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidRelativeKey(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || maxLength <= 0
            || value.Length > maxLength
            || HasInvalidText(value)
            || value[0] is '/' or '\\'
            || value.Contains('\\', StringComparison.Ordinal)
            || IsWindowsRootedPath(value))
        {
            return false;
        }

        return value.Split('/').All(static segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static bool IsWindowsRootedPath(string value) =>
        value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':';

    private static bool HasInvalidText(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsControl(value[index]))
            {
                return true;
            }

            if (char.IsSurrogate(value[index]))
            {
                if (!char.IsSurrogatePair(value, index))
                {
                    return true;
                }

                index++;
            }
        }

        return false;
    }
}
