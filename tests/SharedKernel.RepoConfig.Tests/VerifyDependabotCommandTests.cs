namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Tests the verify-dependabot command against workspaces with a <c>.github/dependabot.yml</c> file.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class VerifyDependabotCommandTests
{
    private const string Scoped = """
        version: 2
        updates:
          - package-ecosystem: github-actions
            directory: /
            ignore:
              # Moves with the pin manifest.
              - dependency-name: actions/checkout
                update-types:
                  - version-update:semver-major
                  - version-update:semver-minor
                  - version-update:semver-patch
              - dependency-name: "actions/setup-dotnet"
                update-types: ["version-update:semver-patch"]
          - package-ecosystem: nuget
            directory: /
        """;

    [Fact]
    public async Task Verify_dependabot_accepts_ignore_entries_that_name_update_types_and_configs_without_ignore()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".github/dependabot.yml", Scoped);

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-dependabot", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardError.ShouldBe(string.Empty);
        result.StandardOutput.ShouldBe("Dependabot ignore entries leave security updates enabled." + Environment.NewLine);
    }

    [Theory]
    [InlineData("- dependency-name: actions/checkout", 6, "actions/checkout")]
    [InlineData("- dependency-name: \"*\"", 6, "*")]
    [InlineData("- dependency-name: 'tj-actions/changed-files' # pinned", 6, "tj-actions/changed-files")]
    public async Task Verify_dependabot_rejects_an_ignore_entry_without_update_types(string entry, int line, string name)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".github/dependabot.yml", $"version: 2\nupdates:\n  - package-ecosystem: github-actions\n    directory: /\n    ignore:\n      {entry}\n");

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-dependabot", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldBe(
            "Dependabot ignore verification failed:" + Environment.NewLine
            + $"- .github/dependabot.yml: Line {line}: The ignore entry '{name}' has no update-types, so it also blocks security updates; add the version-update types to ignore." + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_dependabot_reports_only_the_unscoped_entry_among_scoped_ones()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".github/dependabot.yml", Scoped.Replace("update-types: [\"version-update:semver-patch\"]", "versions: [\"1.x\"]", StringComparison.Ordinal));

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-dependabot", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Line 12: The ignore entry 'actions/setup-dotnet' has no update-types", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("actions/checkout", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_dependabot_fails_when_the_file_is_missing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".github/dependabot.yml", Scoped);
        workspace.DeleteFile(".github/dependabot.yml");

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-dependabot", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: The root has no .github/dependabot.yml or .github/dependabot.yaml file." + Environment.NewLine);
    }
}
