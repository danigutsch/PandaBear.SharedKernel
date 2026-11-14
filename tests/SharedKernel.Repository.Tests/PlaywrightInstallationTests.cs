using System.Text.RegularExpressions;
using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Guards the <c>install-playwright</c> command, which finds the built playwright.ps1 installer of a test project and runs it with PowerShell.</summary>
public sealed class PlaywrightInstallationTests : IDisposable
{
    private const string Project = "tests/Core.Tests/Core.Tests.csproj";

    private readonly RepositoryTestScope scope = new();

    private readonly StringWriter log = new();

    [Theory]
    [InlineData("bin\\Debug\\net10.0\\")]
    [InlineData("bin/Debug/net10.0/")]
    [InlineData("bin\\Debug/net10.0/")]
    public async Task The_installer_is_found_through_the_evaluated_output_path_with_either_separator(string outputPath)
    {
        // Arrange
        scope.WriteFile("tests/Core.Tests/bin/Debug/net10.0/playwright.ps1", "# installer");
        var processes = new ProcessLog((fileName, _, _) => Task.FromResult(fileName == "dotnet" ? outputPath + Environment.NewLine : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root,
            ["install-playwright", "--project", Project, "--browser", "chromium", "--browser", "firefox", "--with-dependencies"],
            processes.Run,
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldBe(
        [
            "dotnet msbuild " + Path.GetFullPath(Path.Combine(scope.Root, Project)) + " -getProperty:OutputPath",
            "pwsh " + Path.Combine(scope.Root, "tests", "Core.Tests", "bin", "Debug", "net10.0", "playwright.ps1") + " install --with-deps chromium firefox",
        ]);
        processes.Calls.ShouldAllSatisfy(call => call.WorkingDirectory.ShouldBe(scope.Root));
    }

    [Fact]
    public async Task Banner_lines_before_the_evaluated_output_path_are_ignored()
    {
        // Arrange
        // DOTNET_CLI_CONTEXT_VERBOSE, which the CI workflow sets, makes the dotnet CLI print a telemetry line before the property value.
        scope.WriteFile("tests/Core.Tests/bin/Release/net10.0/playwright.ps1", "# installer");
        var processes = new ProcessLog((fileName, _, _) => Task.FromResult(fileName == "dotnet" ? "Telemetry is: Enabled\nbin\\Release/net10.0/\n" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["install-playwright", "--project", Project], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldContain("pwsh " + Path.Combine(scope.Root, "tests", "Core.Tests", "bin", "Release", "net10.0", "playwright.ps1") + " install");
    }

    [Fact]
    public async Task An_absolute_output_path_and_a_configuration_are_honored_without_dependencies()
    {
        // Arrange
        var output = Path.Combine(scope.Root, "artifacts", "bin", "Core.Tests", "release");
        scope.WriteFile("artifacts/bin/Core.Tests/release/playwright.ps1", "# installer");
        var processes = new ProcessLog((fileName, _, _) => Task.FromResult(fileName == "dotnet" ? output : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["install-playwright", "--project", Project, "--configuration", "Release"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldBe(
        [
            "dotnet msbuild " + Path.GetFullPath(Path.Combine(scope.Root, Project)) + " -getProperty:OutputPath -p:Configuration=Release",
            "pwsh " + Path.Combine(output, "playwright.ps1") + " install",
        ]);
    }

    [Fact]
    public async Task A_project_without_a_built_installer_fails_before_starting_powershell()
    {
        // Arrange
        var processes = new ProcessLog(static (fileName, _, _) => Task.FromResult(fileName == "dotnet" ? "bin/Debug/net10.0/" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["install-playwright", "--project", Project], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("playwright.ps1 is not in the build output of " + Project + "; build it first.", StringComparison.Ordinal);
        processes.Calls.ShouldAllSatisfy(static call => call.FileName.ShouldBe("dotnet"));
    }

    [Theory]
    [InlineData("--browser", "--with-deps")]
    [InlineData("--browser", "Chromium")]
    [InlineData("--configuration", "")]
    public async Task Option_values_that_could_change_the_installer_command_fail_to_parse(string option, string value)
    {
        // Arrange
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["install-playwright", "--project", Project, option, value], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        processes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Only_the_headless_shell_is_installed_when_requested()
    {
        // Arrange
        scope.WriteFile("tests/Core.Tests/bin/Debug/net10.0/playwright.ps1", "# installer");
        var processes = new ProcessLog((fileName, _, _) => Task.FromResult(fileName == "dotnet" ? "bin/Debug/net10.0/" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root,
            ["install-playwright", "--project", Project, "--browser", "chromium", "--with-dependencies", "--only-shell"],
            processes.Run,
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands[1].ShouldBe(
            "pwsh " + Path.Combine(scope.Root, "tests", "Core.Tests", "bin", "Debug", "net10.0", "playwright.ps1") + " install --with-deps --only-shell chromium");
    }

    [Fact]
    public async Task The_output_reports_how_long_each_step_took()
    {
        // Arrange
        scope.WriteFile("tests/Core.Tests/bin/Debug/net10.0/playwright.ps1", "# installer");
        var processes = new ProcessLog((fileName, _, _) => Task.FromResult(fileName == "dotnet" ? "bin/Debug/net10.0/" : "installed"));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["install-playwright", "--project", Project], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        Regex.IsMatch(result.Error, @"^playwright: msbuild evaluation took \d+\.\ds; install took \d+\.\ds").ShouldBeTrue();
        result.Error.ShouldContain("installed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stalled_installer_is_stopped_with_a_timeout()
    {
        // Arrange
        scope.WriteFile("tests/Core.Tests/bin/Debug/net10.0/playwright.ps1", "# installer");

        // Act
        var action = async () => await PlaywrightInstallation.Run(
            scope.Root,
            Project,
            [],
            false,
            null,
            async (fileName, _, _, ct) =>
            {
                if (fileName == "dotnet")
                {
                    return "bin/Debug/net10.0/";
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return string.Empty;
            },
            TextWriter.Null,
            TestContext.Current.CancellationToken,
            TimeSpan.FromMilliseconds(100));

        // Assert
        (await action.ShouldThrow<TimeoutException>()).Message.ShouldContain("did not finish within", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_installer_that_exits_non_zero_is_retried_until_it_succeeds()
    {
        // Arrange
        scope.WriteFile("tests/Core.Tests/bin/Debug/net10.0/playwright.ps1", "# installer");
        var installs = 0;
        var processes = new ProcessLog((fileName, _, _) => fileName == "dotnet"
            ? Task.FromResult("bin/Debug/net10.0/")
            : ++installs < 3
                ? Task.FromException<string>(new ProcessExitException(100, "Command failed (100): pwsh playwright.ps1 install"))
                : Task.FromResult("installed"));
        var time = new ImmediateTimeProvider(TimeSpan.FromMinutes(1));

        // Act
        var output = await PlaywrightInstallation.Run(
            scope.Root, Project, ["chromium"], true, null, processes.Run, log, TestContext.Current.CancellationToken, timeProvider: time);

        // Assert
        output.ShouldContain("installed", StringComparison.Ordinal);
        processes.Commands.Count(static command => command.StartsWith("pwsh ", StringComparison.Ordinal)).ShouldBe(3);
        time.Timers.ShouldBe([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)]);
        log.ToString().ShouldContain("playwright: install attempt 1 of 3 failed with exit code 100; retrying in 5s.", StringComparison.Ordinal);
        log.ToString().ShouldContain("playwright: install attempt 2 of 3 failed with exit code 100; retrying in 15s.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_installer_that_keeps_failing_stops_after_three_attempts_with_its_error()
    {
        // Arrange
        scope.WriteFile("tests/Core.Tests/bin/Debug/net10.0/playwright.ps1", "# installer");
        var processes = new ProcessLog(static (fileName, _, _) => fileName == "dotnet"
            ? Task.FromResult("bin/Debug/net10.0/")
            : Task.FromException<string>(new ProcessExitException(100, "Command failed (100): pwsh playwright.ps1 install")));
        var time = new ImmediateTimeProvider(TimeSpan.FromMinutes(1));
        var action = async () => await PlaywrightInstallation.Run(
            scope.Root, Project, ["chromium"], true, null, processes.Run, log, TestContext.Current.CancellationToken, timeProvider: time);

        // Act
        var exception = await action.ShouldThrow<ProcessExitException>();

        // Assert
        exception.Message.ShouldBe("Command failed (100): pwsh playwright.ps1 install");
        processes.Commands.Count(static command => command.StartsWith("pwsh ", StringComparison.Ordinal)).ShouldBe(3);
        time.Timers.ShouldBe([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)]);
        log.ToString().ShouldNotContain("attempt 3", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_installer_that_fails_to_start_is_not_retried()
    {
        // Arrange
        scope.WriteFile("tests/Core.Tests/bin/Debug/net10.0/playwright.ps1", "# installer");
        var processes = new ProcessLog(static (fileName, _, _) => fileName == "dotnet"
            ? Task.FromResult("bin/Debug/net10.0/")
            : Task.FromException<string>(new InvalidOperationException("Failed to start pwsh.")));
        var time = new ImmediateTimeProvider(TimeSpan.FromMinutes(1));
        var action = async () => await PlaywrightInstallation.Run(
            scope.Root, Project, ["chromium"], true, null, processes.Run, log, TestContext.Current.CancellationToken, timeProvider: time);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Failed to start pwsh.");
        processes.Commands.Count(static command => command.StartsWith("pwsh ", StringComparison.Ordinal)).ShouldBe(1);
        time.Timers.ShouldBeEmpty();
        log.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_project_without_a_built_installer_is_not_retried()
    {
        // Arrange
        var processes = new ProcessLog(static (fileName, _, _) => Task.FromResult(fileName == "dotnet" ? "bin/Debug/net10.0/" : string.Empty));
        var time = new ImmediateTimeProvider(TimeSpan.FromMinutes(1));
        var action = async () => await PlaywrightInstallation.Run(
            scope.Root, Project, ["chromium"], true, null, processes.Run, log, TestContext.Current.CancellationToken, timeProvider: time);

        // Act
        await action.ShouldThrow<InvalidOperationException>();

        // Assert
        processes.Commands.ShouldHaveSingleItem().ShouldStartWith("dotnet msbuild ", StringComparison.Ordinal);
        time.Timers.ShouldBeEmpty();
        log.ToString().ShouldBeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        log.Dispose();
        scope.Dispose();
    }
}
