using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>
/// Checks the findings of a large text against the secrets it holds. A pattern match that exceeds its timeout twice in a
/// row, which pauses of the test process on a busy machine can cause, flags its text for review instead of reporting the
/// secrets in it, and the flagged text differs from run to run. A secret therefore counts as covered when it is reported
/// or lies in text that a timeout flagged.
/// </summary>
internal static class ScanCoverage
{
    /// <summary>
    /// Asserts that every finding is a finding of <paramref name="kind"/> at one of <paramref name="secrets"/> or a
    /// pattern timeout, and that every secret is reported unless a pattern timeout flagged its text.
    /// </summary>
    /// <param name="findings">Findings of the scan.</param>
    /// <param name="secrets">Start and length of every secret in the scanned text.</param>
    /// <param name="kind">Kind of the secrets.</param>
    public static void ShouldReportEverySecretOrFlagItForReview(IReadOnlyList<SecretFinding> findings, IReadOnlyList<(int Start, int Length)> secrets, SecretKind kind)
    {
        SecretFinding[] flagged = [.. findings.Where(finding => finding.Kind == SecretKind.NeedsReview)];
        SecretFinding[] reported = [.. findings.Where(finding => finding.Kind != SecretKind.NeedsReview)];
        var starts = secrets.Select(secret => secret.Start).ToHashSet();
        int covered = secrets.Count(secret => flagged.Any(finding => Overlaps(finding.Location, secret)));

        secrets.ShouldNotBeEmpty();
        flagged.ShouldAllSatisfy(finding => finding.Message.ShouldContain("timed out", StringComparison.Ordinal));
        reported.ShouldAllSatisfy(finding =>
        {
            finding.Kind.ShouldBe(kind);
            starts.Contains(finding.Location.Start).ShouldBeTrue();
        });
        reported.Length.ShouldBe(secrets.Count - covered);
    }

    private static bool Overlaps(SecretLocation location, (int Start, int Length) secret) =>
        secret.Start < location.Start + location.Length && location.Start < secret.Start + secret.Length;
}
