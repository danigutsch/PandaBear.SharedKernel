using System.Text;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>One account decoded from an <c>otpauth-migration://</c> payload.</summary>
/// <param name="Name">Account name.</param>
/// <param name="Issuer">Issuer.</param>
/// <param name="IsSupported">Whether Bitwarden can use the entry: TOTP with SHA-1, SHA-256, or SHA-512.</param>
/// <param name="Uri">Normalized <c>otpauth://totp</c> URI holding the secret, when supported.</param>
/// <param name="Problem">Why the entry is unsupported, without any value.</param>
internal sealed record MigratedOtp(string Name, string Issuer, bool IsSupported, string? Uri, string? Problem)
{
    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Issuer = ").Append(Issuer).Append(", IsSupported = ").Append(IsSupported).Append(", Problem = ").Append(Problem);
        return true;
    }
}
