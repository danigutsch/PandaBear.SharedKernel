namespace PandaBear.Idempotency.EntityFrameworkCore;

/// <summary>
/// Validates the opaque retention owner key of an entry. Only ASCII letters, digits and <c>. _ : -</c> are accepted so
/// names, e-mail addresses and free text cannot be stored by accident, the same rule the disposition hold identifiers use.
/// </summary>
internal static class RetentionOwnerIdentifier
{
    /// <summary>The maximum accepted length, which is also the column length.</summary>
    public const int MaxLength = 128;

    /// <summary>Trims the value and requires a non-empty identifier within <see cref="MaxLength" /> that uses only the permitted characters.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <returns>The trimmed value.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is empty or white-space after trimming, is longer than <see cref="MaxLength"/> characters, or contains a character other than ASCII letters, digits, and <c>. _ : -</c>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string Require(string? value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
        }

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"The value cannot exceed {MaxLength} characters.", parameterName);
        }

        foreach (var character in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or ':' or '-'))
            {
                throw new ArgumentException(
                    "The value may contain only ASCII letters, digits and the characters . _ : -.",
                    parameterName);
            }
        }

        return trimmed;
    }
}
