using System.Diagnostics;

namespace PandaBear.Processes.Tests;

/// <summary>Tests removing inherited variables from a child process environment.</summary>
public sealed class ProcessEnvironmentTests
{
    [Fact]
    public void Named_variables_are_removed_in_any_case()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("/bin/true");
        startInfo.Environment.Clear();
        startInfo.Environment["GH_TOKEN"] = "a";
        startInfo.Environment["github_token"] = "b";
        startInfo.Environment["PATH"] = "/usr/bin";

        // Act
        ProcessEnvironment.Remove(startInfo, ["GH_TOKEN", "GITHUB_TOKEN"]);

        // Assert
        startInfo.Environment.Keys.ShouldBe(["PATH"]);
    }

    [Fact]
    public void Git_variables_are_removed_in_any_case()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("/usr/bin/git");
        startInfo.Environment.Clear();
        startInfo.Environment["GIT_DIR"] = "/elsewhere";
        startInfo.Environment["git_index_file"] = "/elsewhere/index";
        startInfo.Environment["GITHUB_SHA"] = "abc";
        startInfo.Environment["HOME"] = "/home/user";

        // Act
        ProcessEnvironment.RemoveGitVariables(startInfo);

        // Assert
        startInfo.Environment.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["GITHUB_SHA", "HOME"]);
    }

    [Fact]
    public void Forge_token_variables_are_the_github_enterprise_and_forgejo_tokens()
    {
        // Assert
        ProcessEnvironment.ForgeTokenVariableNames.ShouldBe(["GH_TOKEN", "GITHUB_TOKEN", "FORGEJO_TOKEN", "GH_ENTERPRISE_TOKEN", "GITHUB_ENTERPRISE_TOKEN"]);
    }

    [Theory]
    [InlineData("GH_TOKEN")]
    [InlineData("gh_token")]
    [InlineData("GITHUB_TOKEN")]
    [InlineData("Github_Token")]
    [InlineData("FORGEJO_TOKEN")]
    [InlineData("forgejo_token")]
    [InlineData("GH_ENTERPRISE_TOKEN")]
    [InlineData("gh_enterprise_token")]
    [InlineData("GITHUB_ENTERPRISE_TOKEN")]
    [InlineData("Github_Enterprise_Token")]
    public void Every_forge_token_variable_is_removed_in_any_case(string name)
    {
        // Arrange
        var startInfo = new ProcessStartInfo("/bin/true");
        startInfo.Environment.Clear();
        startInfo.Environment[name] = "credential-marker";
        startInfo.Environment["PATH"] = "/usr/bin";

        // Act
        ProcessEnvironment.RemoveForgeTokens(startInfo);

        // Assert
        startInfo.Environment.Keys.ShouldBe(["PATH"]);
    }

    [Fact]
    public void All_forge_token_variables_are_removed_together_and_other_variables_are_kept()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("/bin/true");
        startInfo.Environment.Clear();
        foreach (var name in ProcessEnvironment.ForgeTokenVariableNames)
        {
            startInfo.Environment[name] = "credential-marker";
        }

        startInfo.Environment["GITHUB_SHA"] = "abc";
        startInfo.Environment["FORGEJO_TOKEN_FILE"] = "/run/token";
        startInfo.Environment["HOME"] = "/home/user";

        // Act
        ProcessEnvironment.RemoveForgeTokens(startInfo);

        // Assert
        startInfo.Environment.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["FORGEJO_TOKEN_FILE", "GITHUB_SHA", "HOME"]);
    }

    [Theory]
    [InlineData("GH_TOKEN", true)]
    [InlineData("github_token", true)]
    [InlineData("Forgejo_Token", true)]
    [InlineData("GH_ENTERPRISE_TOKEN", true)]
    [InlineData("github_enterprise_token", true)]
    [InlineData("GH_ENTERPRISE_TOKEN_FILE", false)]
    [InlineData("GH_HOST", false)]
    [InlineData("GH_CONFIG_DIR", false)]
    [InlineData("GITHUB_SHA", false)]
    [InlineData("FORGEJO_TOKEN_FILE", false)]
    [InlineData("", false)]
    public void Forge_token_variables_are_recognized_in_any_case(string name, bool expected)
    {
        // Act
        var recognized = ProcessEnvironment.IsForgeTokenVariable(name);

        // Assert
        recognized.ShouldBe(expected);
    }

    [Fact]
    public async Task A_child_started_after_the_removal_receives_no_forge_token()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var startInfo = new ProcessStartInfo("/usr/bin/env") { RedirectStandardOutput = true, RedirectStandardError = true };
        startInfo.Environment.Clear();
        foreach (var name in ProcessEnvironment.ForgeTokenVariableNames)
        {
            startInfo.Environment[name] = "credential-marker";
            startInfo.Environment[name.Replace("_TOKEN", "_Token", StringComparison.Ordinal)] = "credential-marker";
        }

        startInfo.Environment["KEPT_VARIABLE"] = "kept";

        // Act
        ProcessEnvironment.RemoveForgeTokens(startInfo);
        var output = await new SystemProcessRunner().Run(startInfo, null, TestContext.Current.CancellationToken);

        // Assert
        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("KEPT_VARIABLE=kept", StringComparison.Ordinal);
        output.StandardOutput.ShouldNotContain("credential-marker", StringComparison.Ordinal);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        // Act
        Action removeWithoutStartInfo = () => ProcessEnvironment.Remove(null!, []);
        Action removeWithoutNames = () => ProcessEnvironment.Remove(new ProcessStartInfo("/bin/true"), null!);
        Action removeGitWithoutStartInfo = () => ProcessEnvironment.RemoveGitVariables(null!);
        Action removeForgeTokensWithoutStartInfo = () => ProcessEnvironment.RemoveForgeTokens(null!);
        Action recognizeWithoutName = () => ProcessEnvironment.IsForgeTokenVariable(null!);

        // Assert
        removeWithoutStartInfo.ShouldThrow<ArgumentNullException>();
        removeWithoutNames.ShouldThrow<ArgumentNullException>();
        removeGitWithoutStartInfo.ShouldThrow<ArgumentNullException>();
        removeForgeTokensWithoutStartInfo.ShouldThrow<ArgumentNullException>();
        recognizeWithoutName.ShouldThrow<ArgumentNullException>();
    }
}
