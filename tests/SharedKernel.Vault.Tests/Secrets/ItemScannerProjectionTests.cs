using SharedKernel.SecretScanning;
using SharedKernel.Vault.Secrets;

namespace SharedKernel.Vault.Tests.Secrets;

/// <summary>Tests how a finding of the text scanner becomes a vault finding with a proposed target.</summary>
public sealed class ItemScannerProjectionTests
{
    [Fact]
    public void A_region_where_a_pattern_timed_out_is_not_moved_and_needs_review()
    {
        // Arrange
        var detected = DetectedSecrets.Of("slow-rule", SecretKind.NeedsReview) with
        {
            TimedOut = true,
            NeedsReview = true,
        };

        // Act
        var finding = ItemScanner.Project(detected, null);

        // Assert
        finding.ProposedTarget.Kind.ShouldBe(PlacementTargetKind.NotMoved);
        finding.NeedsReview.ShouldBeTrue();
    }

    [Fact]
    public void A_value_that_does_not_fit_a_bitwarden_field_needs_review_and_says_why()
    {
        // Act
        var finding = ItemScanner.Project(DetectedSecrets.Of("generic-high-entropy", SecretKind.GenericSecret), new string('k', 20_000));

        // Assert
        finding.NeedsReview.ShouldBeTrue();
        finding.Message.ShouldEndWith("; exceeds the Bitwarden field budget", StringComparison.Ordinal);
        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("Secret"));
    }

    [Fact]
    public void A_finding_without_one_value_is_not_measured_against_the_field_budget()
    {
        // Act
        var finding = ItemScanner.Project(DetectedSecrets.Of("recovery-code-block", SecretKind.RecoveryCodes), null);

        // Assert
        finding.NeedsReview.ShouldBeFalse();
        finding.ProposedTarget.ShouldBe(PlacementTarget.Hidden("Recovery codes"));
    }

    [Fact]
    public void A_catalog_rule_without_a_listed_target_goes_where_its_kind_suggests()
    {
        // Act
        var token = ItemScanner.Project(DetectedSecrets.Of("github-pat", SecretKind.ApiToken) with { RuleName = "GitHub token" }, "ghp_x");
        var keyId = ItemScanner.Project(DetectedSecrets.Of("aws-access-key-id", SecretKind.AccessKeyId) with { RuleName = "AWS access key ID" }, "AKIA");

        // Assert
        token.ProposedTarget.ShouldBe(PlacementTarget.Hidden("GitHub token"));
        keyId.ProposedTarget.ShouldBe(PlacementTarget.Text("AWS access key ID"));
    }

    [Fact]
    public void Every_rule_with_a_listed_target_is_one_the_scanner_reports()
    {
        // Act
        var unknown = ItemScanner.RuleTargets.Keys.Except(SecretScanner.Default.RuleIds);

        // Assert
        unknown.ShouldBeEmpty();
    }
}
