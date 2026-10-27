using PandaBear.VaultManager.Tool.Tests.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Builds secret-shaped pipeline fixtures at test runtime, so no literal codes or seeds live in the repository.</summary>
internal static class PipelineSecrets
{
    /// <summary>Returns the Base32 encoding of 20 pseudo-random bytes derived from the seed.</summary>
    public static string TotpSecret(int seed) => TestSecrets.Base32Encode(TestSecrets.Bytes(seed, 20));

    /// <summary>Wraps the secret in an <c>otpauth://totp</c> URI for the GitHub issuer and account alice.</summary>
    public static string TotpUri(string secret) => "otpauth://totp/GitHub:alice?secret=" + secret + "&issuer=GitHub";

    /// <summary>Sixteen codes shaped like GitHub recovery codes: ten lowercase hex characters split by a dash.</summary>
    public static string[] GitHubRecoveryCodes(int seed) =>
        [.. Enumerable.Range(0, 16).Select(index => TestSecrets.FromAlphabet(TestSecrets.LowerHex, 10, seed + index)).Select(code => code[..5] + "-" + code[5..])];

    /// <summary>Builds notes text with a "GitHub recovery codes" heading, one code per line, and a trailing "Password:" line.</summary>
    public static string RecoveryNotes(string[] codes, string password) =>
        "GitHub recovery codes\n" + string.Join('\n', codes) + "\n\nPassword: " + password;
}
