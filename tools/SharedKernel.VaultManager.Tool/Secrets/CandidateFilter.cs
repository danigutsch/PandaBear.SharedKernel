using System.Net;
using System.Text.RegularExpressions;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Rejects values that look like secrets by shape but are identifiers, timestamps, paths, or placeholders.</summary>
internal static class CandidateFilter
{
    private static readonly string[] PlaceholderWords =
    [
        "changeme", "change_me", "change-me", "password", "passwort", "secret", "token", "example", "sample", "dummy",
        "placeholder", "redacted", "removed", "hidden", "none", "null", "nil", "undefined", "true", "false", "yes", "no",
        "todo", "tbd", "n/a", "na", "unknown", "test", "testing", "default", "empty", "your_api_key", "your-api-key",
        "yourapikey", "your_token", "api_key", "apikey", "letmein", "qwerty",
    ];

    /// <summary>Whether a value must never be reported as a secret.</summary>
    /// <param name="value">Candidate value.</param>
    /// <returns><see langword="true"/> when the value is a known false positive.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool IsRejected(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string trimmed = value.Trim().Trim('"', '\'', '`');
        if (trimmed.Length is 0 or > 4096)
        {
            return true;
        }

        return IsPlaceholder(trimmed)
            || Matches(SecretPatterns.Uuid(), trimmed)
            || Matches(SecretPatterns.DateOrTime(), trimmed)
            || Matches(SecretPatterns.SemVer(), trimmed)
            || Matches(SecretPatterns.Email(), trimmed)
            || Matches(SecretPatterns.PlainUrl(), trimmed)
            || Matches(SecretPatterns.FilePath(), trimmed)
            || IsIpAddress(trimmed)
            || IsRepeatedOrSequential(trimmed);
    }

    /// <summary>Whether a value is a template variable, masked value, or common dummy word.</summary>
    /// <param name="value">Trimmed value.</param>
    /// <returns><see langword="true"/> when the value is a placeholder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool IsPlaceholder(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (PlaceholderWords.Contains(value, StringComparer.OrdinalIgnoreCase)
            || Matches(SecretPatterns.TemplatePlaceholder(), value))
        {
            return true;
        }

        // Masks such as ***, xxxx, ????, or ••••.
        return value.Length >= 3 && value.All(character => character is '*' or 'x' or 'X' or '?' or '.' or '#' or '•' or '-' or '_');
    }

    /// <summary>Whether a value is one character repeated, a short unit repeated, or an ascending or descending run.</summary>
    /// <param name="value">Value.</param>
    /// <returns><see langword="true"/> when the value has no real randomness.</returns>
    public static bool IsRepeatedOrSequential(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length < 4)
        {
            return false;
        }

        for (int unit = 1; unit <= Math.Min(4, value.Length / 2); unit++)
        {
            bool repeats = true;
            for (int index = unit; index < value.Length && repeats; index++)
            {
                repeats = value[index] == value[index - unit];
            }

            if (repeats)
            {
                return true;
            }
        }

        int ascending = 0;
        int descending = 0;
        for (int index = 1; index < value.Length; index++)
        {
            int step = value[index] - value[index - 1];
            ascending += step == 1 ? 1 : 0;
            descending += step == -1 ? 1 : 0;
        }

        return ascending == value.Length - 1 || descending == value.Length - 1;
    }

    private static bool IsIpAddress(string value)
    {
        return (value.Contains('.', StringComparison.Ordinal) || value.Contains(':', StringComparison.Ordinal))
            && value.All(character => char.IsAsciiHexDigit(character) || character is '.' or ':' or '/' or '%')
            && IPAddress.TryParse(value.Split('/')[0], out _);
    }

    // An anchored, bounded filter that times out, even when run again, means the value is unusual; keep it for review.
    private static bool Matches(Regex pattern, string value) =>
        PatternRetry.TryRun(() => pattern.IsMatch(value), out bool matched) && matched;
}
