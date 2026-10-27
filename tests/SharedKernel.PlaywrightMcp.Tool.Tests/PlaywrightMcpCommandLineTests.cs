using System.Diagnostics;
using System.Reflection;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

/// <summary>Tests parsing, help, version, and cancellation handling of the Playwright MCP command line.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class PlaywrightMcpCommandLineTests
{
    /// <summary>Verifies that root help exits 0 and describes the commands, image, and environment variables without starting a process.</summary>
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

    /// <summary>Verifies that help for each command shows its usage and description without starting a process.</summary>
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

    /// <summary>Verifies that <c>--version</c> prints the informational assembly version and starts no process.</summary>
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

    /// <summary>Verifies that every command line in the README parses without errors to the command or action it documents, and that every command is documented.</summary>
    [Fact]
    public void Readme_command_lines_select_their_command_or_the_help_and_version_actions()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        var rootCommand = PlaywrightMcpCommandLine.Create(
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable,
            static (_, _, _) => Task.FromResult(new ProcessResult(0, string.Empty, string.Empty)));
        var commandLines = DocumentedCommandLines.Find(DocumentedCommandLines.ReadToolReadme());

        // Act
        var parsed = commandLines
            .Select(static commandLine => (CommandLine: commandLine, Arguments: DocumentedCommandLines.Arguments(commandLine)))
            .Select(entry => (entry.CommandLine, Expected: entry.Arguments[0], Result: rootCommand.Parse(entry.Arguments)))
            .ToArray();
        var selections = parsed.Select(static entry => DocumentedCommandLines.Selection(entry.Result)).ToArray();

        // Assert
        commandLines.ShouldNotBeEmpty();
        parsed.ShouldAllSatisfy(static entry =>
        {
            entry.Result.Errors.ShouldBeEmpty();
            DocumentedCommandLines.Selection(entry.Result).ShouldBe(entry.Expected);
        });
        rootCommand.Subcommands.ShouldAllSatisfy(command => selections.ShouldContain(command.Name));
    }

    /// <summary>Verifies that the diagram directive prints the parse tree and exits 0 for valid command lines and non-zero otherwise, without starting a process.</summary>
    [Theory]
    [InlineData("serve", 0)]
    [InlineData("serve --hlep", 1)]
    public async Task Diagram_directive_reports_whether_a_command_line_parses_without_starting_a_process(
        string commandLine,
        int expectedExitCode)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(commandLine);
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["[diagram]", .. commandLine.Split(' ')],
            (_, _) =>
            {
                invocation++;
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            },
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(expectedExitCode, "Consumer caller tests read the parse result from the exit code.");
        invocation.ShouldBe(0);
        environment.Output.ToString().ShouldContain("[ serve", StringComparison.Ordinal);
    }

    /// <summary>Verifies that omitting the command exits 1 with a usage message and starts no process.</summary>
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

    /// <summary>Verifies that unrecognized arguments exit 1 with an error and start no process.</summary>
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

    /// <summary>Verifies that each command exits 2 on a remote engine override before starting a process.</summary>
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

    /// <summary>Verifies that cancelling the invocation stops the runtime and removes its container.</summary>
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

    /// <summary>Verifies that <c>prepare</c> and <c>clean</c> report cancellation whether or not the engine fails after being cancelled.</summary>
    [Theory]
    [InlineData("prepare", false)]
    [InlineData("prepare", true)]
    [InlineData("clean", false)]
    [InlineData("clean", true)]
    public async Task Prepare_and_clean_report_cancellation_when_the_engine_stops_after_cancellation(string command, bool engineFails)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            [command],
            async (_, _) =>
            {
                invocation++;
                switch (invocation)
                {
                    case 1:
                        return new ProcessResult(0, "default", string.Empty);
                    case 2:
                        return new ProcessResult(0, "unix:///var/run/docker.sock", string.Empty);
                    default:
                        // The same Ctrl+C stops the engine, which exits or fails before the runner observes the token.
                        await cancellation.CancelAsync();
                        return engineFails
                            ? throw new IOException("The pipe is broken.")
                            : new ProcessResult(130, string.Empty, string.Empty);
                }
            },
            cancellation.Token);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(3);
        environment.Error.ToString().ShouldBe("Error: Playwright MCP was cancelled." + Environment.NewLine);
        environment.Output.ToString().ShouldBeEmpty();
    }

    /// <summary>Verifies that <c>serve</c> removes the container with an uncancelled cleanup token and reports cancellation, whether or not the runtime fails.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Serve_removes_the_container_and_reports_cancellation_when_the_runtime_stops_after_cancellation(bool runtimeFails)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var invocation = 0;
        ProcessStartInfo? cleanup = null;
        var cleanupTokenWasCancelled = true;

        // Act
        var exitCode = await environment.Invoke(
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
                        // The same Ctrl+C stops the runtime, which exits or fails before the runner observes the token.
                        await cancellation.CancelAsync();
                        return runtimeFails
                            ? throw new IOException("The pipe is broken.")
                            : new ProcessResult(130, string.Empty, string.Empty);
                    default:
                        cleanup = startInfo;
                        cleanupTokenWasCancelled = ct.IsCancellationRequested;
                        return new ProcessResult(0, string.Empty, string.Empty);
                }
            },
            cancellation.Token);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(4);
        cleanupTokenWasCancelled.ShouldBeFalse();
        var cleanupStartInfo = cleanup.ShouldNotBeNull();
        cleanupStartInfo.ArgumentList.ShouldContain("rm");
        cleanupStartInfo.ArgumentList.ShouldContain("--force");
        cleanupStartInfo.ArgumentList.ShouldContain(static argument => argument.StartsWith("sharedkernel-playwright-mcp-", StringComparison.Ordinal));
        environment.Error.ToString().ShouldBe("Error: Playwright MCP was cancelled." + Environment.NewLine);
        environment.Output.ToString().ShouldBeEmpty();
    }

    /// <summary>Verifies that a cancelled engine check reports cancellation instead of an engine failure.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Engine_checks_report_cancellation_when_the_engine_stops_after_cancellation(bool engineFails)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var invocation = 0;

        // Act
        var exitCode = await environment.Invoke(
            ["serve"],
            async (_, _) =>
            {
                invocation++;

                // The same Ctrl+C stops the context check, which exits or fails before the runner observes the token.
                await cancellation.CancelAsync();
                return engineFails
                    ? throw new InvalidOperationException("Could not start docker.")
                    : new ProcessResult(130, string.Empty, "interrupted");
            },
            cancellation.Token);

        // Assert
        exitCode.ShouldBe(1);
        invocation.ShouldBe(1);
        environment.Error.ToString().ShouldBe("Error: Playwright MCP was cancelled." + Environment.NewLine);
        environment.Output.ToString().ShouldBeEmpty();
    }
}
