namespace PandaBear.Vault.Secrets;

/// <summary>The rules for a TOTP shared secret in Base32.</summary>
internal static class TotpSecret
{
    /// <summary>Smallest accepted TOTP secret, in bits.</summary>
    public const int MinimumBits = 80;

    /// <summary>Whether text is a strict base32 TOTP secret of at least 80 bits: A-Z, 2-7, and optional padding.</summary>
    /// <param name="value">Candidate, already free of separators.</param>
    /// <returns><see langword="true"/> when the candidate qualifies.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string trimmed = value.TrimEnd('=');
        if (trimmed.Length * 5 < MinimumBits)
        {
            return false;
        }

        foreach (char character in trimmed)
        {
            if (character is not ((>= 'A' and <= 'Z') or (>= '2' and <= '7')))
            {
                return false;
            }
        }

        return true;
    }
}
