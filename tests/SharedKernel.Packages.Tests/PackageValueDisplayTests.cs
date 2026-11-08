using static PandaBear.Packages.Tests.PackageFactory;

namespace PandaBear.Packages.Tests;

/// <summary>Tests how the small package values print and compare.</summary>
[Trait("Capability", "Packages")]
public sealed class PackageValueDisplayTests
{
    private static readonly DateTimeOffset Released = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_installed_package_prints_as_name_equals_version_and_equals_one_with_the_same_parts()
    {
        // Act
        var first = Installed("git", "1:2.43");
        var second = Installed("git", "1:2.43");

        // Assert
        first.ToString().ShouldBe("git=1:2.43");
        first.ShouldBe(second);
        first.ShouldNotBe(Installed("git", "1:2.44"));
    }

    [Fact]
    public void A_requirement_prints_its_name_and_adds_the_version_only_when_pinned()
    {
        // Act
        var plain = Require("git");
        var pinned = Require("git", "2.43");

        // Assert
        plain.ToString().ShouldBe("git");
        pinned.ToString().ShouldBe("git=2.43");
        plain.ShouldNotBe(pinned);
        plain.Version.ShouldBeNull();
    }

    [Fact]
    public void A_change_prints_its_kind_and_requirement_and_the_installed_version_only_for_a_version_change()
    {
        // Act
        var install = new PackageChange(PackageChangeKind.Install, Require("git"));
        var change = new PackageChange(PackageChangeKind.ChangeVersion, Require("git", "2.44"), Version("2.43"));

        // Assert
        install.ToString().ShouldBe("Install git");
        change.ToString().ShouldBe("ChangeVersion git=2.44 (installed 2.43)");
        install.InstalledVersion.ShouldBeNull();
        change.InstalledVersion.ShouldBe(Version("2.43"));
        install.ShouldNotBe(change);
    }

    [Fact]
    public void A_candidate_exposes_its_parts_prints_the_move_and_compares_by_value_including_the_release_time()
    {
        // Act
        var candidate = Candidate("bat", "0.24.0", "0.26.1", Released);

        // Assert
        candidate.Package.ShouldBe(Name("bat"));
        candidate.InstalledVersion.ShouldBe(Version("0.24.0"));
        candidate.CandidateVersion.ShouldBe(Version("0.26.1"));
        candidate.ReleasedAt.ShouldBe(Released);
        candidate.ToString().ShouldBe("bat 0.24.0 -> 0.26.1");
        candidate.ShouldBe(Candidate("bat", "0.24.0", "0.26.1", Released));
        candidate.ShouldNotBe(Candidate("bat", "0.24.0", "0.26.1", Released.AddDays(1)));
    }

    [Fact]
    public void A_decision_prints_its_kind_candidate_and_reason_and_compares_by_value()
    {
        // Act
        var decision = new UpdateDecision(UpdateDecisionKind.Hold, Candidate("bat", "1", "2", Released), "too new");

        // Assert
        decision.ToString().ShouldBe("Hold: bat 1 -> 2 - too new");
        decision.ShouldBe(new UpdateDecision(UpdateDecisionKind.Hold, Candidate("bat", "1", "2", Released), "too new"));
        decision.ShouldNotBe(new UpdateDecision(UpdateDecisionKind.Allow, Candidate("bat", "1", "2", Released), "too new"));
    }

    [Theory]
    [InlineData(0, "0 days")]
    [InlineData(7, "7 days")]
    [InlineData(MinimumReleaseAge.MaxDays, "3650 days")]
    public void A_minimum_age_inside_the_range_keeps_its_days_prints_them_and_compares_by_value(int days, string text)
    {
        // Act
        var age = MinimumReleaseAge.FromDays(days).ShouldBeSuccess();

        // Assert
        age.Value.ShouldBe(TimeSpan.FromDays(days));
        age.ToString().ShouldBe(text);
        age.ShouldBe(Age(days));
    }

    [Fact]
    public void An_advisory_prints_its_id_severity_and_package_and_lists_its_affected_versions_in_order()
    {
        // Act
        var advisory = AdvisoryFor("bat", "GHSA-9", "2", "1");

        // Assert
        advisory.ToString().ShouldBe("GHSA-9 (High) bat");
        advisory.Id.ShouldBe("GHSA-9");
        advisory.Package.ShouldBe(Name("bat"));
        advisory.Severity.ShouldBe(AdvisorySeverity.High);
        advisory.AffectedVersions.Select(static v => v.Value).ShouldBe(["1", "2"]);
        advisory.Affects(Version("1")).ShouldBeTrue();
        advisory.Affects(Version("3")).ShouldBeFalse();
    }

    [Fact]
    public void A_version_longer_than_the_limit_is_rejected()
    {
        // Act
        var result = PackageVersion.Create(new string('1', PackageVersion.MaxLength + 1));

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["version"].ShouldBe(["Package version cannot exceed 128 characters."]);
    }

    [Fact]
    public void An_advisory_with_an_unknown_severity_is_rejected()
    {
        // Act
        var result = Advisory.Create("GHSA-1", Name("bat"), (AdvisorySeverity)99, [Version("1")]);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["advisory"].ShouldBe(["Advisory severity is not a known value."]);
    }
}
