namespace PandaBear.Secrets.Tests;

/// <summary>Builds a verification from the recipients a SOPS JSON file names.</summary>
internal static class VerificationChecks
{
    /// <summary>Compares a JSON file encrypted to <paramref name="fileRecipients"/> with <paramref name="rule"/>.</summary>
    public static RecipientVerification Verify(SopsCreationRule rule, params string[] fileRecipients) =>
        RecipientVerification.Compare(SopsMetadataReader.Read(SopsSamples.Json(fileRecipients), SopsFormat.Json).ShouldBeSuccess(), rule);
}
