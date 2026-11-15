namespace PandaBear.PlaywrightMcp.Tests;

/// <summary>Tests the server through its process runner and executable resolver, without starting a container engine.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class PlaywrightMcpServerTests
{
    private const string LocalDockerEndpoint = "unix:///var/run/docker.sock";

    [Fact]
    public async Task Prepare_checks_the_local_docker_context_then_pulls_the_image_into_the_diagnostics()
    {
        // Arrange
        var machine = new FakeMachine();
        var docker = FakeMachine.GetExecutablePath("docker");
        machine.SetExecutable("docker", docker);
        var runner = new ScriptedProcessRunner(
            new ProcessOutput(0, "default"),
            new ProcessOutput(0, LocalDockerEndpoint),
            new ProcessOutput(0, string.Empty));
        using var diagnostics = new StringWriter();

        // Act
        var exitCode = await new PlaywrightMcpServer(runner, machine).Prepare(machine.Settings, diagnostics, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        runner.Runs.Count.ShouldBe(3);
        runner.Runs.ShouldAllSatisfy(run => run.StartInfo.FileName.ShouldBe(docker));
        runner.Runs[2].StartInfo.ArgumentList.ToArray().ShouldBe(["--host", LocalDockerEndpoint, "pull", PlaywrightMcpServer.Image]);
        runner.Runs[2].Forward.ShouldBeSameAs(diagnostics);
    }

    [Fact]
    public async Task Clean_returns_the_exit_code_of_the_image_removal()
    {
        // Arrange
        var machine = new FakeMachine();
        machine.SetExecutable("docker", FakeMachine.GetExecutablePath("docker"));
        var runner = new ScriptedProcessRunner(
            new ProcessOutput(0, "default"),
            new ProcessOutput(0, LocalDockerEndpoint),
            new ProcessOutput(5, string.Empty));
        using var diagnostics = new StringWriter();

        // Act
        var exitCode = await new PlaywrightMcpServer(runner, machine).Clean(machine.Settings, diagnostics, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(5);
        runner.Runs[2].StartInfo.ArgumentList.ToArray().ShouldBe(["--host", LocalDockerEndpoint, "image", "rm", PlaywrightMcpServer.Image]);
    }

    [Fact]
    public async Task Serve_removes_its_container_after_the_server_exits()
    {
        // Arrange
        var machine = new FakeMachine();
        machine.SetExecutable("podman", FakeMachine.GetExecutablePath("podman"));
        var runner = new ScriptedProcessRunner(
            new ProcessOutput(0, "true"),
            new ProcessOutput(0, string.Empty),
            new ProcessOutput(0, string.Empty));
        using var diagnostics = new StringWriter();

        // Act
        var exitCode = await new PlaywrightMcpServer(runner, machine).Serve(machine.Settings, diagnostics, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var run = runner.Runs[1].StartInfo.ArgumentList.ToArray();
        var cleanup = runner.Runs[2].StartInfo.ArgumentList.ToArray();
        run.ShouldContain("run");
        var name = run.Where(static argument => argument.StartsWith("--name=", StringComparison.Ordinal)).ShouldHaveSingleItem()["--name=".Length..];
        name.StartsWith("sharedkernel-playwright-mcp-", StringComparison.Ordinal).ShouldBeTrue();
        cleanup[^1].ShouldBe(name);
        diagnostics.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_remote_docker_override_fails_with_exit_code_2_before_any_process_starts()
    {
        // Arrange
        var machine = new FakeMachine();
        machine.SetExecutable("docker", FakeMachine.GetExecutablePath("docker"));
        machine.SetEnvironmentVariable("DOCKER_HOST", "tcp://remote:2375");
        var runner = new ScriptedProcessRunner();
        using var diagnostics = new StringWriter();

        // Act
        var exitCode = await new PlaywrightMcpServer(runner, machine).Serve(machine.Settings, diagnostics, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        runner.Runs.ShouldBeEmpty();
        diagnostics.ToString().ShouldBe("Error: Refusing Docker host or context overrides for Playwright MCP." + Environment.NewLine);
    }

    [Fact]
    public async Task A_non_local_docker_context_fails_with_exit_code_1()
    {
        // Arrange
        var machine = new FakeMachine();
        machine.SetExecutable("docker", FakeMachine.GetExecutablePath("docker"));
        var runner = new ScriptedProcessRunner(
            new ProcessOutput(0, "remote-builder"),
            new ProcessOutput(0, "tcp://builder.example:2376"));
        using var diagnostics = new StringWriter();

        // Act
        var exitCode = await new PlaywrightMcpServer(runner, machine).Prepare(machine.Settings, diagnostics, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        runner.Runs.Count.ShouldBe(2);
        diagnostics.ToString().ShouldContain("Refusing non-local Docker context 'remote-builder'", StringComparison.Ordinal);
    }

    [Fact]
    public void Null_dependencies_are_rejected()
    {
        // Act
        Action withoutRunner = () => _ = new PlaywrightMcpServer(null!, new FakeMachine());
        Action withoutResolver = () => _ = new PlaywrightMcpServer(new ScriptedProcessRunner(), null!);

        // Assert
        withoutRunner.ShouldThrow<ArgumentNullException>();
        withoutResolver.ShouldThrow<ArgumentNullException>();
    }
}
