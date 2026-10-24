namespace PandaBear.Repository.Tests;

public sealed class RepositoryValidationTests
{
    private const string CoreTests = "tests/Core.Tests/Core.Tests.csproj";

    [Fact]
    public async Task A_slice_restores_builds_and_tests_its_solution_filter_without_capping_test_parallelism()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        var filter = Path.Combine(selection, "library.slnf");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            Path.GetDirectoryName(scope.Root)!, ["validate", "--slice", "library", "--root", scope.Root], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        processes.Commands.ShouldBe(
        [
            "git rev-parse HEAD",
            "dotnet restore " + filter + " --locked-mode",
            "dotnet build " + filter + " -c Release --no-restore",
            "dotnet test --solution " + filter + " -c Release --no-build --no-restore",
        ]);
        processes.Calls.ShouldAllSatisfy(call => call.WorkingDirectory.ShouldBe(scope.Root));
    }

    [Fact]
    public async Task Validation_writes_the_dotnet_output_to_standard_error()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection("fixture", ("library", [CoreTests]));
        var processes = new ProcessLog(static (fileName, arguments, _) =>
            Task.FromResult(fileName == "git" ? "fixture" : "dotnet " + arguments[0] + " output" + Environment.NewLine));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--slice", "library"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(
            "dotnet restore output" + Environment.NewLine + "dotnet build output" + Environment.NewLine + "dotnet test output" + Environment.NewLine);
    }

    [Fact]
    public async Task A_slice_without_selected_tests_reports_it_on_standard_output()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection("fixture", ("library", []));
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--slice", "library"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe("No test projects selected for library" + Environment.NewLine);
        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
    }

    [Fact]
    public async Task Stale_selection_cannot_execute_validation()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection("previous", ("library", [CoreTests]));
        var processes = new ProcessLog(static (fileName, _, _) => Task.FromResult(fileName == "git" ? "current" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--slice", "library"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
        result.Error.ShouldContain("revision", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_failed_build_does_not_run_tests_or_claim_success()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        var processes = new ProcessLog(static (fileName, arguments, _) => fileName == "git"
            ? Task.FromResult("fixture")
            : arguments[0] == "build" ? throw new InvalidOperationException("Build failed.") : Task.FromResult(string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--slice", "library"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Build failed.", StringComparison.Ordinal);
        processes.Commands.Any(command => command.StartsWith("dotnet test", StringComparison.Ordinal)).ShouldBeFalse();
        var timing = await File.ReadAllTextAsync(Path.Combine(selection, "execution-library", "timing.tsv"), ct);
        timing.ShouldContain("\tfailed", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("other", "Unknown slice.")]
    [InlineData("hosted", "The selection has no solution filter for the slice.")]
    public async Task An_unknown_slice_or_a_missing_filter_fails_with_the_domain_exit_code_before_building(string slice, string error)
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]), ("hosted", ["tests/Hosted/Hosted.csproj"]));
        File.Delete(Path.Combine(selection, "hosted.slnf"));
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--slice", slice], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(error, StringComparison.Ordinal);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
    }

    [Fact]
    public async Task Validation_reads_the_selection_directory_under_the_root()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelectionIn("plans/current", "fixture", ("library", [CoreTests]));
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--slice", "library", "--selection", "plans/current"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var timing = await File.ReadAllTextAsync(Path.Combine(selection, "execution-library", "timing.tsv"), ct);
        timing.ShouldBe("revision\tslice\tduration_seconds\tstatus\nfixture\tlibrary\t" + timing.Split('\t')[^2] + "\tpassed\n");
    }

    [Fact]
    public async Task Cancellation_stops_the_running_process_and_records_the_failed_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var processes = new ProcessLog(async (fileName, arguments, processToken) =>
        {
            if (fileName == "dotnet" && arguments[0] == "build")
            {
                await cancellation.CancelAsync();
                await Task.Delay(Timeout.Infinite, processToken);
            }

            return fileName == "git" ? "fixture" : string.Empty;
        });

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--slice", "library"], processes.Run, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldAllSatisfy(call => call.Token.IsCancellationRequested.ShouldBeTrue());
        processes.Commands.Any(command => command.StartsWith("dotnet test", StringComparison.Ordinal)).ShouldBeFalse();
        var timing = await File.ReadAllTextAsync(Path.Combine(selection, "execution-library", "timing.tsv"), ct);
        timing.ShouldContain("\tfailed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_process_failure_after_cancellation_exits_as_the_cancellation_without_output()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var processes = new ProcessLog(async (fileName, arguments, _) =>
        {
            if (fileName == "dotnet" && arguments[0] == "build")
            {
                // Ctrl+C also stops the child process, which can fail before the token is observed.
                await cancellation.CancelAsync();
                throw new InvalidOperationException("Command failed (130): dotnet build");
            }

            return fileName == "git" ? "fixture" : string.Empty;
        });

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--slice", "library"], processes.Run, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
        processes.Commands.Any(command => command.StartsWith("dotnet test", StringComparison.Ordinal)).ShouldBeFalse();
        var timing = await File.ReadAllTextAsync(Path.Combine(selection, "execution-library", "timing.tsv"), ct);
        timing.ShouldContain("\tfailed", StringComparison.Ordinal);
    }
}
