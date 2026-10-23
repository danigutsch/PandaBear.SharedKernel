using System.Diagnostics;
using System.Reflection;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class PlaywrightMcpCommandLineTests
{
    [Fact]
    public async Task Help_describes_the_commands_image_and_environment_without_starting_a_process()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["--help"],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        invocation.ShouldBe(0);
        var help = environment.Output.ToString();
        help.ShouldContain("  serve ", StringComparison.Ordinal);
        help.ShouldContain("  prepare ", StringComparison.Ordinal);
        help.ShouldContain("  clean ", StringComparison.Ordinal);
        help.ShouldContain("PLAYWRIGHT_MCP_ENGINE", StringComparison.Ordinal);
        help.ShouldContain("PLAYWRIGHT_MCP_NETWORK_ACCESS", StringComparison.Ordinal);
        help.ShouldContain("PLAYWRIGHT_MCP_IMAGE_RESPONSES", StringComparison.Ordinal);
        var unwrappedHelp = string.Concat(help.Where(static character => !char.IsWhiteSpace(character)));
        unwrappedHelp.ShouldContain(PlaywrightMcpCommand.Image, StringComparison.Ordinal);
        environment.Error.ToString().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("serve", "Run the pinned Playwright MCP image as a stdio MCP server")]
    [InlineData("prepare", "Pull the pinned Playwright MCP image")]
    [InlineData("clean", "Remove the pinned Playwright MCP image")]
    public async Task Command_help_describes_the_command_without_starting_a_process(
        string command,
        string description)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            [command, "--help"],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        invocation.ShouldBe(0);
        var help = environment.Output.ToString();
        help.ShouldContain($" {command} [options]", StringComparison.Ordinal);
        help.ShouldContain(description, StringComparison.Ordinal);
        environment.Error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Version_prints_the_tool_version_without_starting_a_process()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        var invocation = 0;
        var expectedVersion = Assembly.GetEntryAssembly().ShouldNotBeNull()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>().ShouldNotBeNull()
            .InformationalVersion;

        // Act
        var exitCode = await environment.Invoke(
            ["--version"],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        invocation.ShouldBe(0);
        environment.Output.ToString().ShouldBe(expectedVersion + Environment.NewLine);
        environment.Error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_command_fails_parsing_before_any_process_starts()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            [],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(0);
        environment.Error.ToString().ShouldContain("Required command was not provided.", StringComparison.Ordinal);
        environment.Output.ToString().ShouldContain("Usage:", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("serve", "--allow-unrestricted-file-access")]
    [InlineData("serve", "--headless")]
    [InlineData("prepare", "--force")]
    [InlineData("clean", "extra")]
    public async Task Unrecognized_arguments_fail_parsing_before_any_process_starts(
        string command,
        string argument)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            [command, argument],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(0);
        environment.Error.ToString().ShouldContain(
            $"Unrecognized command or argument '{argument}'",
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("serve")]
    [InlineData("prepare")]
    [InlineData("clean")]
    public async Task Every_command_refuses_remote_engine_overrides_before_any_process_starts(string command)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        environment.SetEnvironmentVariable("DOCKER_HOST", "tcp://remote.example:2375");
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            [command],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        invocation.ShouldBe(0);
        environment.Error.ToString().ShouldBe(
            "Error: Refusing Docker host or context overrides for Playwright MCP." + Environment.NewLine);
        environment.Output.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancelling_the_invocation_stops_the_runtime_and_removes_the_container()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var runtimeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocation = 0;
        ProcessStartInfo? cleanup = null;
        var cleanupTokenWasCancelled = true;

        // Act
        var execution = environment.Invoke(
            ["serve"],
            async (startInfo, ct) =>
            {
                invocation++;
                switch (invocation)
                {
                    case 1:
                        return new ProcessResult(0, "default", string.Empty);
                    case 2:
                        return new ProcessResult(0, "unix:///var/run/docker.sock", string.Empty);
                    case 3:
                        runtimeStarted.SetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                        return new ProcessResult(0, string.Empty, string.Empty);
                    default:
                        cleanup = startInfo;
                        cleanupTokenWasCancelled = ct.IsCancellationRequested;
                        return new ProcessResult(0, string.Empty, string.Empty);
                }
            },
            cancellation.Token);
        await Task.WhenAny(runtimeStarted.Task, execution)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        var exitCode = await execution.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(4);
        cleanupTokenWasCancelled.ShouldBeFalse();
        var cleanupStartInfo = cleanup.ShouldNotBeNull();
        cleanupStartInfo.ArgumentList.ShouldContain("rm");
        cleanupStartInfo.ArgumentList.ShouldContain("--force");
        environment.Error.ToString().ShouldBe("Error: Playwright MCP was cancelled." + Environment.NewLine);
        environment.Output.ToString().ShouldBeEmpty();
    }
}
