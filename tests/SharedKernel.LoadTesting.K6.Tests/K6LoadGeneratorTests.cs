using System.ComponentModel;

namespace PandaBear.LoadTesting.K6.Tests;

/// <summary>Tests how the k6 generator starts k6 and the container engine, through a scripted process runner.</summary>
public sealed class K6LoadGeneratorTests
{
    private const string Image = "grafana/k6:1.0.0@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Local_k6_exports_the_summary_receives_the_variables_and_keeps_only_platform_variables()
    {
        // Arrange
        using var workspace = new K6RunWorkspace();
        var runner = new ScriptedProcessRunner(ScriptedProcessRunner.Exit(99));
        var run = workspace.Run(environment: new Dictionary<string, string>(StringComparer.Ordinal) { ["PATHEXT"] = ".EXE", ["GH_TOKEN"] = "secret", ["FORGEJO_TOKEN"] = "secret", ["HOME"] = "/home/user" });

        // Act
        var exitCode = await new K6LoadGenerator(runner).Run(run, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(99);
        var startInfo = runner.Runs.ShouldHaveSingleItem();
        startInfo.FileName.ShouldBe("/usr/bin/k6");
        startInfo.WorkingDirectory.ShouldBe(workspace.Root);
        startInfo.ArgumentList.ShouldBe(
        [
            "run",
            "--no-usage-report",
            "--summary-export",
            Path.Combine(run.RunDirectory, "k6-summary.json"),
            "-e",
            "APP_BASE_URL=http://localhost:5000",
            "-e",
            "K6_NO_USAGE_REPORT=true",
            "--vus",
            "2",
            run.ScriptPath,
        ]);
        startInfo.Environment.Keys.ShouldBe(["PATHEXT"]);
    }

    [Theory]
    [InlineData("podman", new string[0])]
    [InlineData("docker", new[] { "--add-host", "host.docker.internal:host-gateway" })]
    public async Task Container_k6_mounts_the_scripts_read_only_and_never_pulls(string engine, string[] hostArguments)
    {
        // Arrange
        using var workspace = new K6RunWorkspace();
        var runner = new ScriptedProcessRunner(K6RunWorkspace.WritesSummary(0));
        var run = workspace.Run(new LoadTestContainer(engine, Image), "/usr/bin/" + engine);

        // Act
        var exitCode = await new K6LoadGenerator(runner).Run(run, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var arguments = runner.Runs.ShouldHaveSingleItem().ArgumentList.ToArray();
        arguments[4].ShouldMatch(@"\Asharedkernel-k6-[0-9a-f]{32}\z");
        arguments.ShouldBe(
        [
            "run",
            "--rm",
            "--pull=never",
            "--name",
            arguments[4],
            .. hostArguments,
            "--mount",
            "type=bind,source=" + run.ScriptDirectory + ",target=/k6,readonly",
            "--mount",
            "type=bind,source=" + run.RunDirectory + ",target=/results",
            "-w",
            "/k6",
            Image,
            "run",
            "--no-usage-report",
            "--summary-export",
            "/results/k6-summary.json",
            "-e",
            "APP_BASE_URL=http://localhost:5000",
            "-e",
            "K6_NO_USAGE_REPORT=true",
            "--vus",
            "2",
            "/k6/scenarios/smoke.js",
        ]);
        runner.Runs[0].Environment.ContainsKey("GH_TOKEN").ShouldBeFalse();
        runner.Runs[0].Environment.ContainsKey("GITHUB_TOKEN").ShouldBeFalse();
        runner.Runs[0].Environment.ContainsKey("FORGEJO_TOKEN").ShouldBeFalse();
    }

    [Fact]
    public async Task A_container_run_that_exits_0_without_a_summary_fails()
    {
        // Arrange
        using var workspace = new K6RunWorkspace();
        var runner = new ScriptedProcessRunner(ScriptedProcessRunner.Exit(0));

        // Act
        var exception = await Record.ExceptionAsync(() => new K6LoadGenerator(runner).Run(workspace.Run(new LoadTestContainer("podman", Image), "/usr/bin/podman"), TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe(
            "k6 exited 0 but wrote no k6-summary.json in the results directory of the run. The container user must be able to write the results directory.");
    }

    [Fact]
    public async Task A_failed_container_run_returns_its_exit_code_without_a_summary()
    {
        // Arrange
        using var workspace = new K6RunWorkspace();
        var runner = new ScriptedProcessRunner(ScriptedProcessRunner.Exit(7));

        // Act
        var exitCode = await new K6LoadGenerator(runner).Run(workspace.Run(new LoadTestContainer("podman", Image), "/usr/bin/podman"), TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(7);
    }

    [Fact]
    public async Task Cancellation_force_removes_the_container()
    {
        // Arrange
        using var workspace = new K6RunWorkspace();
        var runner = new ScriptedProcessRunner(
            ScriptedProcessRunner.Throw(new OperationCanceledException()),
            ScriptedProcessRunner.Exit(0));

        // Act
        var exception = await Record.ExceptionAsync(() => new K6LoadGenerator(runner).Run(workspace.Run(new LoadTestContainer("docker", Image), "/usr/bin/docker"), TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeOfType<OperationCanceledException>();
        runner.Runs.Count.ShouldBe(2);
        var name = runner.Runs[0].ArgumentList[4];
        runner.Runs[1].FileName.ShouldBe("/usr/bin/docker");
        runner.Runs[1].ArgumentList.ShouldBe(["rm", "--force", name]);
        runner.Runs[1].RedirectStandardOutput.ShouldBeTrue();
        runner.Runs[1].RedirectStandardError.ShouldBeTrue();
        workspace.Error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task A_failed_removal_names_the_container_to_remove()
    {
        // Arrange
        using var workspace = new K6RunWorkspace();
        var runner = new ScriptedProcessRunner(
            ScriptedProcessRunner.Throw(new OperationCanceledException()),
            ScriptedProcessRunner.Throw(new Win32Exception("gone")));

        // Act
        await Record.ExceptionAsync(() => new K6LoadGenerator(runner).Run(workspace.Run(new LoadTestContainer("podman", Image), "/usr/bin/podman"), TestContext.Current.CancellationToken));

        // Assert
        var name = runner.Runs[0].ArgumentList[4];
        workspace.Error.ToString().ShouldBe($"Container {name} may still be running. Remove it with: podman rm --force {name}\n");
    }

    [Fact]
    public async Task A_k6_that_cannot_start_fails_with_the_operating_system_reason()
    {
        // Arrange
        using var workspace = new K6RunWorkspace();
        var missing = Path.Combine(workspace.Root, "missing", "k6");

        // Act
        var exception = await Record.ExceptionAsync(() => new K6LoadGenerator(new SystemProcessRunner()).Run(workspace.Run(executable: missing), TestContext.Current.CancellationToken));

        // Assert
        var failure = exception.ShouldBeOfType<InvalidOperationException>();
        failure.Message.ShouldStartWith("Failed to start " + missing + ": ", StringComparison.Ordinal);
        failure.InnerException.ShouldBeOfType<Win32Exception>();
    }

    [Fact]
    public void Validation_applies_the_settings_permissions()
    {
        // Arrange
        using var workspace = new K6RunWorkspace();
        var generator = new K6LoadGenerator(new ScriptedProcessRunner());
        var settings = new LoadTestSettings { Root = workspace.Root, ConfigurationPath = ".config/sharedkernel-k6.json", Scenario = "smoke", AllowHttpDebug = true };

        // Act
        var allowed = Record.Exception(() => generator.ValidateArguments(["--http-debug"], [], settings));
        var rejected = Record.Exception(() => generator.ValidateArguments(["--out", "json=x.json"], [], settings));

        // Assert
        allowed.ShouldBeNull();
        rejected.ShouldBeOfType<InvalidOperationException>();
        generator.Command.ShouldBe("k6");
        generator.ControlledVariables.ShouldBe([new("K6_NO_USAGE_REPORT", "true")]);
    }
}
