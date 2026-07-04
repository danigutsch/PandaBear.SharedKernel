namespace SharedKernel.Versioning.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, "Versioning")]
public static class ConventionalCommitParserTests
{
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

    [Theory]
    [InlineData("feat: add option", ReleaseImpact.Minor)]
    [InlineData("fix: correct version", ReleaseImpact.Patch)]
    [InlineData("perf: reduce allocations", ReleaseImpact.Patch)]
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
