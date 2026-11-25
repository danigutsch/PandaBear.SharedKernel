using System.Text.RegularExpressions;

namespace SharedKernel.SecretScanning.Tests;

/// <summary>Tests that a pattern that times out flags the text it ran on for review instead of hiding it.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.SecretScanningCapability)]
public sealed class SecretScannerTimeoutTests
{
    [Fact]
    public void A_pattern_that_times_out_twice_produces_needs_review_finding()
    {
        // Arrange
        var rule = new SecretRule
        {
            Id = "slow-rule",
            DisplayName = "Slow rule",
            Kind = SecretKind.ApiToken,
            Keywords = ["aaaa"],
            Pattern = SlowPatterns.Catastrophic(),
            Source = "test",
        };
        var scanner = new SecretScanner([rule]);

        // Act
        var findings = scanner.Scan(new string('a', 4000), "notes", SecretScanContext.None);

        // Assert
        var finding = findings.ShouldHaveSingleItem(found => found.RuleId == "slow-rule");
        finding.Kind.ShouldBe(SecretKind.NeedsReview);
        finding.NeedsReview.ShouldBeTrue();
        finding.TimedOut.ShouldBeTrue();
    }


    [Fact]
    public void A_timeout_in_one_window_still_flags_the_later_windows_of_the_rule()
    {
        // Arrange
        var rule = new SecretRule
        {
            Id = "slow-rule",
            DisplayName = "Slow rule",
            Kind = SecretKind.ApiToken,
            Keywords = ["aaaa"],
            Pattern = SlowPatterns.Catastrophic(),
            Source = "test",
        };
        var scanner = new SecretScanner([rule]);
        string run = new('a', 4000);
        const int SecondRun = 24_000;
        string notes = run + new string('x', SecondRun - run.Length) + run;

        // Act
        var findings = scanner.Scan(notes, "notes", SecretScanContext.None);

        // Assert
        DetectedSecret[] timeouts = [.. findings.Where(found => found.RuleId == "slow-rule")];
        timeouts.ShouldAllSatisfy(found => found.Kind.ShouldBe(SecretKind.NeedsReview));
        timeouts.ShouldContain(found => found.Location.Start == 0);
        timeouts.ShouldContain(found => found.Location.Start < SecondRun && found.Location.Start + found.Location.Length >= notes.Length);
    }


    [Fact]
    public void A_window_whose_match_timed_out_once_runs_again_and_reports_its_finding()
    {
        // Arrange
        int checks = 0;
        var rule = new SecretRule
        {
            Id = "paused-rule",
            DisplayName = "Paused rule",
            Kind = SecretKind.ApiToken,
            Keywords = ["tok_"],
            Pattern = TestPatterns.Token(),

            // The first check times out, as a match does when a pause of the process outlasts the match timeout.
            Validator = _ => ++checks == 1 ? throw new RegexMatchTimeoutException() : true,
            Source = "test",
        };
        var scanner = new SecretScanner([rule]);

        // Act
        var findings = scanner.Scan("deploy token tok_" + new string('k', 16), "notes", SecretScanContext.None);

        // Assert
        var finding = findings.ShouldHaveSingleItem(found => found.RuleId == "paused-rule");
        finding.Kind.ShouldBe(SecretKind.ApiToken);
        finding.NeedsReview.ShouldBeFalse();
        checks.ShouldBe(2);
    }


    [Fact]
    public void The_remaining_windows_of_a_rule_become_one_finding_once_three_of_its_windows_time_out()
    {
        // Arrange
        // Runs of "a" 10,000 characters apart give the rule five separate windows: each run's window starts 1024 characters
        // before it, and the last one ends with the text.
        const int Spacing = 10_000;
        string run = new('a', 4000);
        string notes = string.Join(new string('x', Spacing - run.Length), Enumerable.Repeat(run, 5));
        var scanner = new SecretScanner([SlowPatterns.Rule()]);

        // Act
        var findings = scanner.Scan(notes, "notes", SecretScanContext.None);

        // Assert
        findings.ShouldAllSatisfy(found =>
        {
            found.RuleId.ShouldBe("slow-rule");
            found.Kind.ShouldBe(SecretKind.NeedsReview);
        });
        int[] starts = [.. findings.Select(found => found.Location.Start)];
        starts.ShouldBe([0, Spacing - 1024, (2 * Spacing) - 1024, (3 * Spacing) - 1024]);
        var rest = findings[^1];
        int restEnd = rest.Location.Start + rest.Location.Length;
        restEnd.ShouldBe(notes.Length);
        rest.Message.ShouldBe("Slow rule pattern timed out 3 times; review the rest of this text manually");
    }
}
