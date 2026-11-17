namespace PandaBear.Secrets.Tests;

/// <summary>Tests creation rules, their regular expressions, and first-match-wins selection.</summary>
[Trait("Capability", "Secrets")]
public sealed class SopsCreationRuleTests
{
    [Fact]
    public void A_rule_matches_paths_by_an_unanchored_expression()
    {
        // Arrange
        var rule = TestRecipients.Rule(@"secrets/.*\.sops\.ya?ml$", 2, TestRecipients.First, TestRecipients.Second);

        // Assert
        rule.Matches(TestRecipients.File("secrets/app.sops.yaml")).ShouldBeTrue();
        rule.Matches(TestRecipients.File("deploy/secrets/app.sops.yml")).ShouldBeTrue();
        rule.Matches(TestRecipients.File("other/app.sops.yaml")).ShouldBeFalse();
        rule.Matches(TestRecipients.File("secrets/app.sops.json")).ShouldBeFalse();
    }

    [Fact]
    public void The_first_matching_rule_wins()
    {
        // Arrange
        var specific = TestRecipients.Rule(@"^secrets/prod/", 2, TestRecipients.First, TestRecipients.Second);
        var general = TestRecipients.Rule(@"^secrets/", 1, TestRecipients.Third);
        var ordered = SopsCreationRuleSet.Create([specific, general]).ShouldBeSuccess();
        var reversed = SopsCreationRuleSet.Create([general, specific]).ShouldBeSuccess();
        var path = TestRecipients.File("secrets/prod/app.sops.yaml");

        // Assert
        ordered.FirstMatch(path).ShouldBe(specific);
        reversed.FirstMatch(path).ShouldBe(general);
        ordered.FirstMatch(TestRecipients.File("secrets/dev/app.sops.yaml")).ShouldBe(general);
        ordered.FirstMatch(TestRecipients.File("elsewhere/app.sops.yaml")).ShouldBeNull();
        ordered.Rules.Count.ShouldBe(2);
    }

    [Fact]
    public void The_minimum_is_a_parameter_of_the_rule()
    {
        // Arrange
        var one = TestRecipients.Set(TestRecipients.First);
        var two = TestRecipients.Set(TestRecipients.First, TestRecipients.Second);

        // Assert
        SopsCreationRule.Create("x", one, 1).IsSuccess.ShouldBeTrue();
        SopsCreationRule.Create("x", one, 2).IsFailure.ShouldBeTrue();
        SopsCreationRule.Create("x", two, 2).IsSuccess.ShouldBeTrue();
        SopsCreationRule.Create("x", two, 3).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65)]
    public void A_minimum_outside_the_range_is_rejected(int minimum)
    {
        // Act
        var result = SopsCreationRule.Create("x", TestRecipients.Set(TestRecipients.First), minimum);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().ContainsKey("minimumRecipients").ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("(")]
    [InlineData("[a-")]
    [InlineData("(a)\\1")]
    [InlineData("(?=secrets)")]
    [InlineData("(?<!x)y")]
    public void A_missing_broken_or_backtracking_only_expression_is_rejected_without_echoing_it(string? pattern)
    {
        // Act
        var result = SopsCreationRule.Create(pattern, TestRecipients.Set(TestRecipients.First), 1);

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["pathRegex"].ShouldHaveSingleItem();
        if (!string.IsNullOrEmpty(pattern))
        {
            message.ShouldNotContain(pattern, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void An_expression_longer_than_the_limit_is_rejected()
    {
        // Act
        var result = SopsCreationRule.Create(new string('a', SopsCreationRule.MaxPatternLength + 1), TestRecipients.Set(TestRecipients.First), 1);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_catastrophic_looking_expression_still_matches_in_linear_time()
    {
        // Arrange
        var rule = TestRecipients.Rule("^(a+)+$", 1, TestRecipients.First);
        var path = TestRecipients.File(new string('a', 500) + "!.sops.yaml");

        // Act
        var matches = rule.Matches(path);

        // Assert
        matches.ShouldBeFalse();
    }

    [Fact]
    public void Missing_recipients_are_rejected()
    {
        // Act
        var result = SopsCreationRule.Create("x", null, 1);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().ContainsKey("recipients").ShouldBeTrue();
    }

    [Fact]
    public void Rules_with_the_same_pattern_recipients_and_minimum_are_equal()
    {
        // Assert
        TestRecipients.Rule("x", 1, TestRecipients.First).ShouldBe(TestRecipients.Rule("x", 1, TestRecipients.First));
        TestRecipients.Rule("x", 1, TestRecipients.First).ShouldNotBe(TestRecipients.Rule("y", 1, TestRecipients.First));
        TestRecipients.Rule("x", 1, TestRecipients.First).ShouldNotBe(TestRecipients.Rule("x", 1, TestRecipients.Second));
    }

    [Fact]
    public void A_rule_set_rejects_null_and_null_rules_and_may_be_empty()
    {
        // Assert
        SopsCreationRuleSet.Create(null).IsFailure.ShouldBeTrue();
        SopsCreationRuleSet.Create([null]).IsFailure.ShouldBeTrue();
        SopsCreationRuleSet.Create([]).ShouldBeSuccess().FirstMatch(TestRecipients.File("a.sops.yaml")).ShouldBeNull();
    }

    [Fact]
    public void Matching_rejects_null()
    {
        // Assert
        Action act1 = () => TestRecipients.Rule("x", 1, TestRecipients.First).Matches(null!);
        act1.ShouldThrow<ArgumentNullException>();
        Action act2 = () => SopsCreationRuleSet.Create([]).ShouldBeSuccess().FirstMatch(null!);
        act2.ShouldThrow<ArgumentNullException>();
    }
}
