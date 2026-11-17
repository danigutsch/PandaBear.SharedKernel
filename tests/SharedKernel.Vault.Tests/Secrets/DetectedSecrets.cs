using PandaBear.SecretScanning;

namespace PandaBear.Vault.Tests.Secrets;

/// <summary>Builds findings of the text scanner for tests.</summary>
internal static class DetectedSecrets
{
    /// <summary>A high-confidence finding of <paramref name="ruleId"/> at the start of the notes.</summary>
    public static DetectedSecret Of(string ruleId, SecretKind kind) => new()
    {
        RuleId = ruleId,
        Kind = kind,
        Confidence = Confidence.High,
        Location = new SecretLocation("notes", 0, 4),
        Message = "found",
    };
}
