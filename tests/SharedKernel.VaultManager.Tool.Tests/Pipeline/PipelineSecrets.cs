using PandaBear.VaultManager.Tool.Tests.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Builds secret-shaped pipeline fixtures at test runtime, so no literal codes or seeds live in the repository.</summary>
internal static class PipelineSecrets
{
    public static string TotpSecret(int seed) => TestSecrets.Base32Encode(TestSecrets.Bytes(seed, 20));

    public static string TotpUri(string secret) => "otpauth://totp/GitHub:alice?secret=" + secret + "&issuer=GitHub";

    /// <summary>Sixteen codes shaped like GitHub recovery codes: ten lowercase hex characters split by a dash.</summary>
    public static string[] GitHubRecoveryCodes(int seed) =>
        [.. Enumerable.Range(0, 16).Select(index => TestSecrets.FromAlphabet(TestSecrets.LowerHex, 10, seed + index)).Select(code => code[..5] + "-" + code[5..])];

    public static string RecoveryNotes(string[] codes, string password) =>
        "GitHub recovery codes\n" + string.Join('\n', codes) + "\n\nPassword: " + password;
}
