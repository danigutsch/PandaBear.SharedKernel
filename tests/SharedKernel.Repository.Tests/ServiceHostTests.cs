using System.Diagnostics;
using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Guards what the CI tool hands a service process and how it reads the value the process writes.</summary>
public sealed class ServiceHostTests
{
    // PATH exists in every environment, so the test can see the value variable removed from the inherited environment.
    private static readonly ServiceConfiguration Service = new("tests/Host/Host.csproj", "APP_READY_FILE", "PATH");

    [Fact]
    public void The_service_runs_dotnet_run_on_the_built_project_from_the_root_with_all_three_streams_redirected()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "sharedkernel-service-root");

        // Act
        var startInfo = ServiceHost.CreateStartInfo(Service, "Debug", root, Path.Combine(root, "ready"));

        // Assert
        Path.GetFileNameWithoutExtension(startInfo.FileName).ShouldBe("dotnet");
        Path.IsPathFullyQualified(startInfo.FileName).ShouldBeTrue("The command resolves from the absolute directories of PATH.");
        startInfo.ArgumentList.ShouldBe(["run", "--project", "tests/Host/Host.csproj", "-c", "Debug", "--no-build", "--no-restore", "--no-launch-profile"]);
        startInfo.WorkingDirectory.ShouldBe(root);
        startInfo.RedirectStandardInput.ShouldBeTrue("The service stops when its input reaches end-of-file.");
        startInfo.RedirectStandardOutput.ShouldBeTrue();
        startInfo.RedirectStandardError.ShouldBeTrue();
        startInfo.UseShellExecute.ShouldBeFalse();
    }

    [Fact]
    public void The_service_environment_names_the_ready_file_and_holds_no_value_variable()
    {
        // Arrange
        var readyFile = Path.Combine(Path.GetTempPath(), "sharedkernel-service-ready");

        // Act
        var startInfo = ServiceHost.CreateStartInfo(Service, "Release", Path.GetTempPath(), readyFile);

        // Assert
        startInfo.Environment["APP_READY_FILE"].ShouldBe(readyFile);
        startInfo.Environment.ContainsKey("PATH").ShouldBeFalse("The service creates what the value stands for, so it must not borrow an inherited one.");
    }

    [Fact]
    public void Tokens_never_reach_the_service_in_any_case()
    {
        // Arrange
        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.Environment["GH_TOKEN"] = "token";
        startInfo.Environment["GITHUB_TOKEN"] = "token";
        startInfo.Environment["github_token"] = "token";
        startInfo.Environment["SHAREDKERNEL_UNRELATED"] = "kept";

        // Act
        ServiceHost.RemoveTokens(startInfo);

        // Assert
        startInfo.Environment.Keys.ShouldNotContain(
            static name => name.Equals("GH_TOKEN", StringComparison.OrdinalIgnoreCase) || name.Equals("GITHUB_TOKEN", StringComparison.OrdinalIgnoreCase),
            "These variables hold a GitHub token.");
        startInfo.Environment["SHAREDKERNEL_UNRELATED"].ShouldBe("kept");
    }

    [Theory]
    [InlineData("Host=localhost;Password=secret\n", "Host=localhost;Password=secret")]
    [InlineData("Host=localhost;Password=secret", "Host=localhost;Password=secret")]
    [InlineData("  Host=localhost  \r\nignored second line\n", "Host=localhost")]
    [InlineData("\uFEFFHost=localhost\n", "Host=localhost")]
    [InlineData("", null)]
    [InlineData("\n", null)]
    [InlineData("   \nHost=localhost\n", null)]
    public void The_value_is_the_trimmed_first_line_of_the_ready_file_and_an_empty_line_is_not_a_value(string text, string? expected)
    {
        // Act
        var value = ServiceHost.ParseValue(text, "database");

        // Assert
        value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Host=local\thost\n")]
    [InlineData("Host=local\0host\n")]
    [InlineData("Host=local\u001bhost\n")]
    public void A_value_with_a_control_character_is_refused_without_repeating_it(string text)
    {
        // Act
        Action parse = () => ServiceHost.ParseValue(text, "database");
        var exception = parse.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldBe("The ready file of the service of slice database must hold one line without control characters.");
    }
}
