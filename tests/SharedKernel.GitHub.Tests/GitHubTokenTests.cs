using System.Diagnostics;
using SharedKernel.Processes;

namespace SharedKernel.GitHub.Tests;

/// <summary>Tests reading the GitHub token and keeping it from child processes.</summary>
public sealed class GitHubTokenTests
{
    [Theory]
    [InlineData("gh", "github", "gh")]
    [InlineData(null, "github", "github")]
    [InlineData("", "github", "github")]
    [InlineData(" ", "github", "github")]
    [InlineData(null, null, null)]
    [InlineData("", " ", null)]
    public void The_first_non_blank_token_wins_in_gh_order(string? ghToken, string? gitHubToken, string? expected)
    {
        // Arrange
        Dictionary<string, string?> variables = new(StringComparer.Ordinal) { ["GH_TOKEN"] = ghToken, ["GITHUB_TOKEN"] = gitHubToken };

        // Act
        var token = GitHubToken.Find(name => variables.GetValueOrDefault(name));

        // Assert
        token.ShouldBe(expected);
    }

    [Fact]
    public void Enterprise_server_tokens_are_never_read_for_github_com()
    {
        // Arrange
        Dictionary<string, string?> variables = new(StringComparer.Ordinal)
        {
            ["GH_ENTERPRISE_TOKEN"] = "enterprise",
            ["GITHUB_ENTERPRISE_TOKEN"] = "enterprise",
        };

        // Act
        var token = GitHubToken.Find(name => variables.GetValueOrDefault(name));

        // Assert
        token.ShouldBeNull();
        GitHubToken.VariableNames.ShouldBe(["GH_TOKEN", "GITHUB_TOKEN"]);
    }

    [Theory]
    [InlineData("GH_TOKEN", true)]
    [InlineData("github_token", true)]
    [InlineData("GH_ENTERPRISE_TOKEN", true)]
    [InlineData("gh_enterprise_token", true)]
    [InlineData("Github_Enterprise_Token", true)]
    [InlineData("GITHUB_SHA", false)]
    public void Token_variables_are_recognized_in_any_case(string name, bool expected)
    {
        // Act
        var isToken = GitHubToken.IsTokenVariable(name);

        // Assert
        isToken.ShouldBe(expected);
    }

    [Fact]
    public void Token_variables_are_removed_from_a_child_environment()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("/bin/true");
        startInfo.Environment.Clear();
        startInfo.Environment["GH_TOKEN"] = "a";
        startInfo.Environment["Github_Token"] = "b";
        startInfo.Environment["gh_enterprise_token"] = "c";
        startInfo.Environment["GitHub_Enterprise_Token"] = "d";
        startInfo.Environment["HOME"] = "/home/user";

        // Act
        GitHubToken.RemoveFrom(startInfo);

        // Assert
        startInfo.Environment.Keys.ShouldBe(["HOME"]);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        // Act
        Action find = () => GitHubToken.Find(null!);
        Action isToken = () => GitHubToken.IsTokenVariable(null!);
        Action remove = () => GitHubToken.RemoveFrom(null!);

        // Assert
        find.ShouldThrow<ArgumentNullException>();
        isToken.ShouldThrow<ArgumentNullException>();
        remove.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Forge_neutral_removal_covers_every_token_variable()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("child");
        foreach (var name in GitHubToken.VariableNames)
        {
            startInfo.Environment[name] = "secret";
        }

        // Act
        ProcessEnvironment.RemoveForgeTokens(startInfo);

        // Assert
        GitHubToken.VariableNames.ShouldAllSatisfy(static name => ProcessEnvironment.ForgeTokenVariableNames.ShouldContain(name));
        startInfo.Environment.Keys.ShouldNotContain(name => GitHubToken.IsTokenVariable(name));
    }

    [Fact]
    public void Removal_covers_every_forge_token_variable()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("child");
        startInfo.Environment.Clear();
        foreach (var name in ProcessEnvironment.ForgeTokenVariableNames)
        {
            startInfo.Environment[name] = "secret";
        }

        // Act
        GitHubToken.RemoveFrom(startInfo);

        // Assert
        startInfo.Environment.Keys.ShouldBeEmpty();
        ProcessEnvironment.ForgeTokenVariableNames.ShouldAllSatisfy(static name => GitHubToken.IsTokenVariable(name).ShouldBeTrue(name));
    }
}
