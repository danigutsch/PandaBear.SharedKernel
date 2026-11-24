using PandaBear.Processes;

namespace PandaBear.Repositories.Tests;

/// <summary>Verifies the evidence and stopping rules for the ordered minimum search.</summary>
public sealed class NuGetAuditSearchTests
{
    [Fact]
    public async Task Search_stops_before_a_later_success_if_an_earlier_attempt_is_indeterminate()
    {
        // Arrange
        NuGetVersionSet[] catalog = [new("Owner", "complete", ["1.1.0", "1.2.0", "2.0.0"])];
        var outcomes = new Queue<NuGetAuditAttemptResult>(
            [NuGetAuditAttemptResult.Rejected, NuGetAuditAttemptResult.Unavailable, NuGetAuditAttemptResult.Verified]);
        List<string> attempts = [];

        // Act
        var result = await NuGetAuditMinimum.Search(NuGetAuditMinimum.Candidates(catalog, includeUnchanged: false),
            (candidate, _) =>
            {
                attempts.Add(candidate[0].Value);
                return Task.FromResult(outcomes.Dequeue());
            }, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(("candidateUnavailable", 2));
        attempts.ShouldBe(["1.1.0", "1.2.0"]);
        outcomes.ShouldHaveSingleItem().ShouldBe(NuGetAuditAttemptResult.Verified);
    }

    [Fact]
    public async Task Search_does_not_attempt_a_candidate_above_its_limit()
    {
        // Arrange
        var versions = Enumerable.Range(1, NuGetAuditMinimum.TrialLimit + 1)
            .Select(version => FormattableString.Invariant($"1.{version}.0")).ToArray();
        NuGetVersionSet[] catalog = [new("Owner", "complete", versions)];
        List<string> tried = [];

        // Act
        var result = await NuGetAuditMinimum.Search(NuGetAuditMinimum.Candidates(catalog, includeUnchanged: false),
            (candidate, _) =>
            {
                tried.Add(candidate[0].Value);
                return Task.FromResult(NuGetAuditAttemptResult.Rejected);
            }, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(("limitReached", NuGetAuditMinimum.TrialLimit));
        var attempted = tried.ToArray();
        attempted.ShouldBe(versions[..^1]);
    }

    [Theory]
    [InlineData("pinRequired", "candidateLockedRestore", 0, "", true)]
    [InlineData("blocked", "build", 1, "error NU1107: conflict", false)]
    [InlineData("candidateUnavailable", "candidateRestore", 1, "error NU1903: advisory", false)]
    [InlineData("blocked", "candidateRestore", 1, "error NU1301: source unreachable", false)]
    [InlineData("blocked", "candidateRestore", 1, "", false)]
    [InlineData("blocked", "candidateLockedRestore", 0, "", false)]
    [InlineData("unresolvedAdvisories", "candidateLockedRestore", 0, "warning NU1903: advisory", true)]
    [InlineData("unresolvedAdvisories", "candidateFileAppLockedRestore", 0, "warning NU1903: advisory", true)]
    [InlineData("blocked", "candidateFileAppLockedRestore", 1, "error NU1107: conflict", false)]
    [InlineData("unresolvedAdvisories", "candidateFileAppLockedRestore", 1, "error NU1903: advisory", false)]
    [InlineData("blocked", "candidateRestore", 1, "error NU1107: conflict", true)]
    [InlineData("blocked", "candidateRestore", 1, "error NU1109: pinned version conflict", true)]
    [InlineData("blocked", "candidateRestore", 1, "error NU1202: framework conflict", true)]
    [InlineData("blocked", "candidateRestore", 1, "error NU1605: downgrade", true)]
    [InlineData("blocked", "candidateRestore", 1, "error NU1608: version constraint", true)]
    [InlineData("blocked", "candidateRestore", 1, "error NU1903: advisory\nerror: restore terminated", false)]
    public void Search_requires_conclusive_restore_evidence(string status, string name, int exitCode, string diagnostics, bool rejected)
    {
        // Arrange
        var step = NuGetTrialStep.Read(name, Path.GetTempPath(), new ProcessOutput(exitCode, diagnostics));

        // Act
        var conclusive = NuGetUpgradeTrial.IsConclusiveRejection(status, step);

        // Assert
        conclusive.ShouldBe(rejected);
    }
}
