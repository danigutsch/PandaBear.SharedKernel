
namespace SharedKernel.CI.Tests;

/// <summary>Guards what the CI tool hands a service process and how it reads the values the process writes.</summary>
public sealed class ServiceHostTests
{
    private static readonly ServiceConfiguration Service = new("tests/Host/Host.csproj", "APP_READY_FILE", ["APP_VALUE", "PATH"]);

    private static readonly string[] Variables = ["APP_DATABASE", "APP_ANALYTICS"];

    private const string Expected = "The ready file of the service of slice database must hold one NAME=value line for each of its value variables (APP_DATABASE, APP_ANALYTICS); ";

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
    public void The_service_environment_names_the_ready_file_and_holds_none_of_the_value_variables()
    {
        // Arrange
        var readyFile = Path.Combine(Path.GetTempPath(), "sharedkernel-service-ready");

        // Act
        var startInfo = ServiceHost.CreateStartInfo(Service, "Release", Path.GetTempPath(), readyFile);

        // Assert
        startInfo.Environment["APP_READY_FILE"].ShouldBe(readyFile);
        startInfo.Environment.ContainsKey("PATH").ShouldBeFalse("The service creates what the values stand for, so it must not borrow an inherited one.");
        startInfo.Environment.ContainsKey("APP_VALUE").ShouldBeFalse();
    }

    [Theory]
    [InlineData("APP_DATABASE=Host=localhost;Password=secret\nAPP_ANALYTICS=Endpoint=http://localhost:3000;WebsiteId=a1\n")]
    [InlineData("APP_ANALYTICS=Endpoint=http://localhost:3000;WebsiteId=a1\nAPP_DATABASE=Host=localhost;Password=secret")]
    [InlineData("﻿APP_DATABASE=Host=localhost;Password=secret\r\nAPP_ANALYTICS=Endpoint=http://localhost:3000;WebsiteId=a1\r\n")]
    [InlineData("APP_DATABASE=  Host=localhost;Password=secret \nAPP_ANALYTICS=\tEndpoint=http://localhost:3000;WebsiteId=a1\n")]
    public void Each_line_hands_its_trimmed_value_to_the_variable_it_names_in_the_order_of_the_configuration(string text)
    {
        // Act
        var values = ServiceHost.ParseValues(text, Variables, "database");

        // Assert
        values.ShouldBe([("APP_DATABASE", "Host=localhost;Password=secret"), ("APP_ANALYTICS", "Endpoint=http://localhost:3000;WebsiteId=a1")]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData(" \r\n \n")]
    [InlineData("﻿")]
    public void A_ready_file_with_nothing_but_white_space_is_not_ready_yet(string text)
    {
        // Act
        var values = ServiceHost.ParseValues(text, Variables, "database");

        // Assert
        values.ShouldBeNull();
    }

    [Theory]
    [InlineData("APP_DATABASE=secret-one\n", "it lacks APP_ANALYTICS.")]
    [InlineData("APP_DATABASE=secret-one\nAPP_ANALYTICS=secret-two\nAPP_OTHER=secret-three\n", "line 3 names none of them.")]
    [InlineData("secret-one\nAPP_ANALYTICS=secret-two\n", "line 1 names none of them.")]
    [InlineData("APP_DATABASE=secret-one\n\nAPP_ANALYTICS=secret-two\n", "line 2 names none of them.")]
    [InlineData(" APP_DATABASE=secret-one\nAPP_ANALYTICS=secret-two\n", "line 1 names none of them.")]
    [InlineData("app_database=secret-one\nAPP_ANALYTICS=secret-two\n", "line 1 names none of them.")]
    [InlineData("APP_DATABASE=secret-one\nAPP_DATABASE=secret-two\nAPP_ANALYTICS=secret-three\n", "line 2 repeats APP_DATABASE.")]
    [InlineData("APP_DATABASE=\nAPP_ANALYTICS=secret-two\n", "line 1 gives APP_DATABASE no value.")]
    [InlineData("APP_DATABASE=secret-one\nAPP_ANALYTICS= \t\n", "line 2 gives APP_ANALYTICS no value.")]
    [InlineData("APP_DATABASE=secret\tone\nAPP_ANALYTICS=secret-two\n", "line 1 gives APP_DATABASE a value with a control character.")]
    [InlineData("APP_DATABASE=secret\u001bone\nAPP_ANALYTICS=secret-two\n", "line 1 gives APP_DATABASE a value with a control character.")]
    [InlineData("APP_DATABASE=secret-one\nAPP_ANALYTICS=secret\0two\n", "line 2 gives APP_ANALYTICS a value with a control character.")]
    public void A_ready_file_that_does_not_hand_over_each_value_variable_once_is_refused_without_repeating_a_value(string text, string problem)
    {
        // Act
        Action parse = () => ServiceHost.ParseValues(text, Variables, "database");
        var exception = parse.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldBe(Expected + problem);
        exception.Message.ShouldNotContain("secret", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("APP_OTHER", StringComparison.Ordinal, "A name that is no value variable may be a value that lost its name.");
    }
}
