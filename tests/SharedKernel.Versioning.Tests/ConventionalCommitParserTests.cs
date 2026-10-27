namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests parsing of Conventional Commit messages.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class ConventionalCommitParserTests
{
    /// <summary>
    /// Verifies that the type, scope, breaking marker, description, body, and footers are parsed from a message.
    /// </summary>
    [Fact]
    public static void Parses_type_scope_bang_description_body_and_footers()
    {
        // Arrange
        const string message = "feat(api)!: add public version endpoint\n\nAdds endpoint metadata.\n\nRefs: release-plan";

        // Act
        var commit = ConventionalCommitParser.Parse(message);

        // Assert
        commit.Type.ShouldBe("feat");
        commit.Scope.ShouldBe("api");
        commit.IsBreakingHeader.ShouldBeTrue();
        commit.Description.ShouldBe("add public version endpoint");
        commit.Body.ShouldBe("Adds endpoint metadata.");
        commit.Footers.ShouldContain("Refs: release-plan");
    }

    /// <summary>
    /// Verifies that commit messages map to the expected release impact, including breaking markers and BREAKING CHANGE footers.
    /// </summary>
    [Theory]
    [InlineData("feat: add option", ReleaseImpact.Minor)]
    [InlineData("fix: correct version", ReleaseImpact.Patch)]
    [InlineData("perf: reduce allocations", ReleaseImpact.Patch)]
    [InlineData("refactor: move shipped parser", ReleaseImpact.Patch)]
    [InlineData("docs: explain releases", ReleaseImpact.None)]
    [InlineData("chore!: drop old output", ReleaseImpact.Major)]
    [InlineData("refactor: move parser\n\nBREAKING CHANGE: parser now rejects invalid headers", ReleaseImpact.Major)]
    [InlineData("build: change package\n\nBREAKING-CHANGE: package id changed", ReleaseImpact.Major)]
    public static void Maps_commit_messages_to_release_impact(string message, ReleaseImpact expectedImpact)
    {
        // Arrange
        // Act
        var commit = ConventionalCommitParser.Parse(message);

        // Assert
        commit.Impact.ShouldBe(expectedImpact);
    }
}
