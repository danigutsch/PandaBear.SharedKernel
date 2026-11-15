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
    public void Null_arguments_are_rejected()
    {
        // Act
        Action removeWithoutStartInfo = () => ProcessEnvironment.Remove(null!, []);
        Action removeWithoutNames = () => ProcessEnvironment.Remove(new ProcessStartInfo("/bin/true"), null!);
        Action removeGitWithoutStartInfo = () => ProcessEnvironment.RemoveGitVariables(null!);

        // Assert
        removeWithoutStartInfo.ShouldThrow<ArgumentNullException>();
        removeWithoutNames.ShouldThrow<ArgumentNullException>();
        removeGitWithoutStartInfo.ShouldThrow<ArgumentNullException>();
    }
}
