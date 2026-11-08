using static PandaBear.Packages.Tests.PackageFactory;

namespace PandaBear.Packages.Tests;

/// <summary>Tests the update policy: advisories first, then the minimum release age.</summary>
[Trait("Capability", "Packages")]
public sealed class UpdatePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_release_older_than_the_minimum_age_is_allowed()
    {
        // Arrange
        var candidate = Candidate("bat", "0.24.0", "0.26.1", Now.AddDays(-10));

        // Act
        var decision = UpdatePolicy.Decide(candidate, [], Age(7), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Allow);
        decision.Reason.ShouldContain("10 days", StringComparison.Ordinal);
    }

    [Fact]
    public void A_release_exactly_as_old_as_the_minimum_age_is_allowed()
    {
        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "1", "2", Now.AddDays(-7)), [], Age(7), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Allow);
    }

    [Fact]
    public void A_release_younger_than_the_minimum_age_is_held_and_says_how_long_to_wait()
    {
        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "1", "2", Now.AddDays(-2)), [], Age(7), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Hold);
        decision.Reason.ShouldContain("in 5 days", StringComparison.Ordinal);
    }

    [Fact]
    public void A_release_dated_in_the_future_is_held_until_it_has_been_public_for_the_minimum_age()
    {
        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "1", "2", Now.AddDays(3)), [], Age(7), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Hold);
        decision.Reason.ShouldContain("in 10 days", StringComparison.Ordinal);
    }

    [Fact]
    public void A_release_a_second_in_the_future_with_no_minimum_age_is_held_for_one_day_in_the_message()
    {
        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "1", "2", Now.AddSeconds(1)), [], Age(0), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Hold);
        decision.Reason.ShouldContain("in 1 day.", StringComparison.Ordinal);
    }

    [Fact]
    public void A_zero_minimum_age_allows_a_release_made_just_now()
    {
        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "1", "2", Now), [], Age(0), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Allow);
    }

    [Fact]
    public void An_advisory_that_affects_the_installed_version_but_not_the_candidate_makes_a_young_release_urgent()
    {
        // Arrange
        var advisory = AdvisoryFor("bat", "GHSA-1", "0.24.0");

        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "0.24.0", "0.26.1", Now.AddHours(-1)), [advisory], Age(7), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Urgent);
        decision.Reason.ShouldContain("GHSA-1", StringComparison.Ordinal);
    }

    [Fact]
    public void A_candidate_affected_by_an_advisory_is_blocked_even_when_it_is_old_enough()
    {
        // Arrange
        var advisory = AdvisoryFor("bat", "GHSA-2", "0.26.1");

        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "0.24.0", "0.26.1", Now.AddDays(-30)), [advisory], Age(7), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Block);
        decision.Reason.ShouldContain("GHSA-2", StringComparison.Ordinal);
    }

    [Fact]
    public void A_block_wins_over_urgent_when_both_versions_are_affected()
    {
        // Arrange
        var advisory = AdvisoryFor("bat", "GHSA-3", "0.24.0", "0.26.1");

        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "0.24.0", "0.26.1", Now.AddDays(-30)), [advisory], Age(7), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Block);
    }

    [Fact]
    public void Advisories_for_other_packages_are_ignored()
    {
        // Arrange
        var advisory = AdvisoryFor("curl", "GHSA-4", "0.24.0", "0.26.1");

        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "0.24.0", "0.26.1", Now.AddDays(-30)), [advisory], Age(7), Now);

        // Assert
        decision.Kind.ShouldBe(UpdateDecisionKind.Allow);
    }

    [Fact]
    public void Several_advisories_are_listed_in_identifier_order()
    {
        // Arrange
        Advisory[] advisories = [AdvisoryFor("bat", "GHSA-b", "0.26.1"), AdvisoryFor("bat", "GHSA-a", "0.26.1")];

        // Act
        var decision = UpdatePolicy.Decide(Candidate("bat", "0.24.0", "0.26.1", Now.AddDays(-30)), advisories, Age(7), Now);

        // Assert
        decision.Reason.ShouldContain("GHSA-a, GHSA-b", StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_inputs_give_an_equal_decision()
    {
        // Arrange
        var candidate = Candidate("bat", "1", "2", Now.AddDays(-2));

        // Act
        var first = UpdatePolicy.Decide(candidate, [], Age(7), Now);
        var second = UpdatePolicy.Decide(candidate, [], Age(7), Now);

        // Assert
        first.ShouldBe(second);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3651)]
    public void A_minimum_age_outside_the_range_is_rejected(int days)
    {
        // Act
        var result = MinimumReleaseAge.FromDays(days);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["minimumReleaseAge"].ShouldHaveSingleItem();
    }

    [Fact]
    public void A_candidate_with_the_installed_version_is_rejected()
    {
        // Act
        var act = () => new UpdateCandidate(Name("bat"), Version("1"), Version("1"), Now);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    public void An_advisory_with_an_invalid_identifier_is_rejected(string? id)
    {
        // Act
        var result = Advisory.Create(id, Name("bat"), AdvisorySeverity.High, [Version("1")]);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["advisory"].ShouldHaveSingleItem();
    }

    [Fact]
    public void An_advisory_without_affected_versions_is_rejected()
    {
        // Act
        var result = Advisory.Create("GHSA-5", Name("bat"), AdvisorySeverity.High, []);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Advisories_with_the_same_versions_in_any_order_are_equal()
    {
        // Act
        var first = AdvisoryFor("bat", "GHSA-6", "1", "2");
        var second = AdvisoryFor("bat", "GHSA-6", "2", "1", "1");

        // Assert
        first.ShouldBe(second);
    }

    [Fact]
    public void An_advisory_identifier_of_exactly_the_longest_allowed_length_is_accepted_and_one_more_is_rejected()
    {
        // Act
        var atLimit = Advisory.Create(new string('A', Advisory.MaxIdLength), Name("bat"), AdvisorySeverity.High, [Version("1")]);
        var over = Advisory.Create(new string('A', Advisory.MaxIdLength + 1), Name("bat"), AdvisorySeverity.High, [Version("1")]);

        // Assert
        atLimit.IsSuccess.ShouldBeTrue();
        over.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["advisory"].ShouldBe(["Advisory identifier cannot exceed 128 characters."]);
    }

    [Theory]
    [InlineData("GHSA\u007f1")]
    [InlineData("GHSA 1")]
    public void An_advisory_identifier_with_a_delete_character_or_space_is_rejected_without_echoing_it(string id)
    {
        // Act
        var result = Advisory.Create(id, Name("bat"), AdvisorySeverity.High, [Version("1")]);

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["advisory"].ShouldHaveSingleItem();
        message.ShouldNotContain(id, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hold_and_an_allow_for_the_same_candidate_are_different_decisions()
    {
        // Arrange
        var candidate = Candidate("bat", "1", "2", Now.AddDays(-2));

        // Act
        var hold = UpdatePolicy.Decide(candidate, [], Age(7), Now);
        var allow = UpdatePolicy.Decide(candidate, [], Age(1), Now);

        // Assert
        hold.Kind.ShouldBe(UpdateDecisionKind.Hold);
        allow.Kind.ShouldBe(UpdateDecisionKind.Allow);
        hold.ShouldNotBe(allow);
    }
}
