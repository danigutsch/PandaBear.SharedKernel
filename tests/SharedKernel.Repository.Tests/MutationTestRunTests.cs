using PandaBear.Testing;

namespace PandaBear.Repository.Tests;

/// <summary>Guards the <c>run-mutation-tests</c> command, which installs the pinned Stryker version and runs each configured Stryker file.</summary>
public sealed class MutationTestRunTests : IDisposable
{
    private const string DomainConfig = "tests/Core.Tests/stryker-domain.json";

    private const string FailingConfig = "tests/Core.Tests/stryker-failing.json";

    private const string OtherConfig = "tests/Other.Tests/stryker-config.json";

    private readonly RepositoryTestScope scope = new();

    private string ToolPath => Path.Combine(scope.Root, "TestResults", "tools", "dotnet-stryker", "4.15.0");

    private string Stryker => Path.Combine(ToolPath, OperatingSystem.IsWindows() ? "dotnet-stryker.exe" : "dotnet-stryker");

    [Fact]
    public async Task Each_config_runs_in_order_from_its_directory_after_the_pinned_version_installs_into_the_scratch_tool_path()
    {
        // Arrange
        scope.ConfigureMutation(DomainConfig, OtherConfig);
        var processes = new ProcessLog((fileName, arguments, _) =>
        {
            if (fileName == "dotnet")
            {
                scope.WriteFile(Path.GetRelativePath(scope.Root, Stryker), "shim");
            }

            return Task.FromResult(fileName == Stryker ? "mutation score " + arguments[^1] : string.Empty);
        });

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run-mutation-tests"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldBe(
        [
            "dotnet tool install dotnet-stryker --version 4.15.0 --tool-path " + ToolPath,
            Stryker + " --config-file stryker-domain.json",
            Stryker + " --config-file stryker-config.json",
        ]);
        processes.Calls.Select(static call => call.WorkingDirectory).ShouldBe(
        [
            scope.Root,
            Path.Combine(scope.Root, "tests", "Core.Tests"),
            Path.Combine(scope.Root, "tests", "Other.Tests"),
        ]);
        result.Output.ShouldContain("==> " + DomainConfig + Environment.NewLine + "mutation score stryker-domain.json", StringComparison.Ordinal);
        result.Output.ShouldContain("==> " + OtherConfig + Environment.NewLine + "mutation score stryker-config.json", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Real_processes_reuse_the_installed_version_and_stop_at_the_first_failing_config()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        scope.ConfigureMutation(DomainConfig, FailingConfig, OtherConfig);
        var log = Path.Combine(scope.Root, "processes.log");
        var fakeBin = Path.Combine(scope.Root, "fake-bin");

        // The fake dotnet installs a fake dotnet-stryker that fails for the failing config.
        FakeExecutable.WriteScript(Path.Combine(fakeBin, "dotnet"), """
            printf '%s|dotnet %s\n' "$PWD" "$*" >> "$FAKE_LOG"
            [ "$1 $2 $3 $4 $5 $6" = "tool install dotnet-stryker --version 4.15.0 --tool-path" ] || exit 9
            mkdir -p "$7"
            cat > "$7/dotnet-stryker" <<'EOF'
            #!/bin/sh
            printf '%s|dotnet-stryker %s\n' "$PWD" "$*" >> "$FAKE_LOG"
            echo "mutation score $2"
            [ "$2" != "stryker-failing.json" ] || exit 3
            EOF
            chmod +x "$7/dotnet-stryker"
            """);
        var environment = new Dictionary<string, string?>
        {
            ["FAKE_LOG"] = log,
            ["PATH"] = fakeBin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
        };
        var ct = TestContext.Current.CancellationToken;

        // Act
        var first = await CiToolProcess.Run(scope.Root, ["run-mutation-tests"], environment, ct);
        var firstLog = await File.ReadAllLinesAsync(log, ct);
        File.Delete(log);
        var second = await CiToolProcess.Run(scope.Root, ["run-mutation-tests"], environment, ct);
        var secondLog = await File.ReadAllLinesAsync(log, ct);

        // Assert
        var core = Path.Combine(scope.Root, "tests", "Core.Tests");
        string[] strykerRuns =
        [
            core + "|dotnet-stryker --config-file stryker-domain.json",
            core + "|dotnet-stryker --config-file stryker-failing.json",
        ];
        first.ExitCode.ShouldBe(2, first.Error);
        firstLog.ShouldBe([scope.Root + "|dotnet tool install dotnet-stryker --version 4.15.0 --tool-path " + ToolPath, .. strykerRuns]);
        first.Output.ShouldContain("mutation score stryker-domain.json", StringComparison.Ordinal);
        first.Output.ShouldNotContain(OtherConfig, StringComparison.Ordinal);
        first.Error.ShouldContain("Command failed (3): " + Stryker + " --config-file stryker-failing.json", StringComparison.Ordinal);
        first.Error.ShouldContain("mutation score stryker-failing.json", StringComparison.Ordinal);
        second.ExitCode.ShouldBe(2, second.Error);
        secondLog.ShouldBe(strykerRuns, "The installed version is reused, so dotnet does not run again.");
    }

    [Fact]
    public async Task A_missing_config_file_fails_before_anything_runs()
    {
        // Arrange
        scope.ConfigureMutation(DomainConfig, OtherConfig);
        File.Delete(Path.Combine(scope.Root, OtherConfig));
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run-mutation-tests"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(".config/sharedkernel-ci.json: $.mutation.configFiles[1] names " + OtherConfig + ", which does not exist.", StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_configuration_without_a_mutation_section_fails()
    {
        // Arrange
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run-mutation-tests"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(".config/sharedkernel-ci.json: $.mutation is required by run-mutation-tests.", StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
