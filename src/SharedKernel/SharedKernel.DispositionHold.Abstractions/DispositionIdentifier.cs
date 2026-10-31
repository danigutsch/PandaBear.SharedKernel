namespace PandaBear.DispositionHold;

/// <summary>
/// Validates the opaque text fields of a hold. Only letters, digits and <c>. _ : -</c> are accepted so free text,
/// names and e-mail addresses cannot be stored by accident.
/// </summary>
internal static class DispositionIdentifier
{
    /// <summary>Trims the value and requires a non-empty identifier within the length limit that uses only the permitted characters.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <param name="maxLength">The maximum accepted length.</param>
    /// <returns>The trimmed value.</returns>
    public static string Require(string? value, string parameterName, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.", parameterName);
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
