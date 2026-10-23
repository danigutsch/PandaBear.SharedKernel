namespace PandaBear.Repository.Tests;

public sealed class RepositoryValidationTests
{
    [Fact]
    public async Task Selected_projects_restore_and_build_as_one_graph()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection(
            ["src/Core/Core.csproj", "src/Dependent/Dependent.csproj", "tests/Core.Tests/Core.Tests.csproj"], ["tests/Core.Tests/Core.Tests.csproj"], "fixture");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            Path.GetDirectoryName(scope.Root)!, ["validate", "--root", scope.Root], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        processes.Commands.Count(command => command.StartsWith("dotnet restore ", StringComparison.Ordinal)).ShouldBe(1);
        processes.Commands.Count(command => command.StartsWith("dotnet build ", StringComparison.Ordinal)).ShouldBe(1);
        processes.Commands.Count(command => command.StartsWith("dotnet test --solution ", StringComparison.Ordinal)).ShouldBe(1);
        processes.Calls.ShouldAllSatisfy(call => call.WorkingDirectory.ShouldBe(scope.Root));
    }

    [Fact]
    public async Task Validation_writes_the_dotnet_output_to_standard_error()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection(["tests/Core.Tests/Core.Tests.csproj"], ["tests/Core.Tests/Core.Tests.csproj"], "fixture");
        var processes = new ProcessLog(static (fileName, arguments, _) =>
            Task.FromResult(fileName == "git" ? "fixture" : "dotnet " + arguments[0] + " output" + Environment.NewLine));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(
            "dotnet restore output" + Environment.NewLine + "dotnet build output" + Environment.NewLine + "dotnet test output" + Environment.NewLine);
    }

    [Fact]
    public async Task Validation_without_selected_projects_reports_it_on_standard_output()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection([], [], "fixture");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe("No projects selected for all" + Environment.NewLine);
        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
    }

    [Fact]
    public async Task Stale_selection_cannot_execute_validation()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection([], [], "previous");
        var processes = new ProcessLog(static (fileName, _, _) => Task.FromResult(fileName == "git" ? "current" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
        result.Error.ShouldContain("revision", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_failed_graph_build_does_not_run_tests_or_claim_success()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection(["tests/Core.Tests/Core.Tests.csproj"], ["tests/Core.Tests/Core.Tests.csproj"], "fixture");
        var processes = new ProcessLog(static (fileName, arguments, _) => fileName == "git"
            ? Task.FromResult("fixture")
            : arguments[0] == "build" ? throw new InvalidOperationException("Build failed.") : Task.FromResult(string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Build failed.", StringComparison.Ordinal);
        processes.Commands.Any(command => command.StartsWith("dotnet test", StringComparison.Ordinal)).ShouldBeFalse();
        var timing = await File.ReadAllTextAsync(Path.Combine(selection, "execution-all", "timing.tsv"), ct);
        timing.ShouldContain("\tfailed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ordinary_group_excludes_hosted_tests_without_capping_test_parallelism()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        scope.AddProject("tests/Hosted/Hosted.csproj", """<Project Sdk="Aspire.AppHost.Sdk" />""");
        var selection = scope.WriteSelection(
            ["tests/Core.Tests/Core.Tests.csproj", "tests/Hosted/Hosted.csproj"], ["tests/Core.Tests/Core.Tests.csproj", "tests/Hosted/Hosted.csproj"], "fixture");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--test-group", "ordinary"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var graph = await File.ReadAllTextAsync(Path.Combine(selection, "execution-ordinary", "build.slnx"), ct);
        graph.ShouldNotContain("Hosted", StringComparison.Ordinal);
        processes.Commands.Any(command => command.Contains("--max-parallel-test-modules", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Hosted_test_group_builds_and_tests_only_that_project()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        scope.AddProject("tests/Hosted/Hosted.csproj", """<Project Sdk="Aspire.AppHost.Sdk" />""");
        var selection = scope.WriteSelection(
            ["tests/Core.Tests/Core.Tests.csproj", "tests/Hosted/Hosted.csproj"], ["tests/Core.Tests/Core.Tests.csproj", "tests/Hosted/Hosted.csproj"], "fixture");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--test-group", "tests/Hosted/Hosted.csproj"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var graph = await File.ReadAllTextAsync(Path.Combine(selection, "execution-Hosted", "build.slnx"), ct);
        graph.ShouldNotContain("Core.Tests", StringComparison.Ordinal);
        processes.Commands.ShouldContain("dotnet test --project tests/Hosted/Hosted.csproj -c Release --no-build --no-restore");
        processes.Commands.Any(command => command.StartsWith("dotnet test --solution", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Unknown_test_group_fails_with_the_domain_exit_code_before_building()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection(["tests/Core.Tests/Core.Tests.csproj"], ["tests/Core.Tests/Core.Tests.csproj"], "fixture");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["validate", "--test-group", "tests/Missing/Missing.csproj"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Unknown test group.", StringComparison.Ordinal);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
    }

    [Fact]
    public async Task Validation_reads_the_selection_directory_under_the_root()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection(["tests/Core.Tests/Core.Tests.csproj"], ["tests/Core.Tests/Core.Tests.csproj"], "fixture", "plans/current");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate", "--selection", "plans/current"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var timing = await File.ReadAllTextAsync(Path.Combine(selection, "execution-all", "timing.tsv"), ct);
        timing.ShouldContain("\tpassed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_stops_the_running_process_and_records_the_failed_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection(["tests/Core.Tests/Core.Tests.csproj"], ["tests/Core.Tests/Core.Tests.csproj"], "fixture");
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
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate"], processes.Run, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldAllSatisfy(call => call.Token.IsCancellationRequested.ShouldBeTrue());
        processes.Commands.Any(command => command.StartsWith("dotnet test", StringComparison.Ordinal)).ShouldBeFalse();
        var timing = await File.ReadAllTextAsync(Path.Combine(selection, "execution-all", "timing.tsv"), ct);
        timing.ShouldContain("\tfailed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_process_failure_after_cancellation_exits_as_the_cancellation_without_output()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection(["tests/Core.Tests/Core.Tests.csproj"], ["tests/Core.Tests/Core.Tests.csproj"], "fixture");
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
        var result = await RepositoryToolInvocation.Run(scope.Root, ["validate"], processes.Run, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
        processes.Commands.Any(command => command.StartsWith("dotnet test", StringComparison.Ordinal)).ShouldBeFalse();
        var timing = await File.ReadAllTextAsync(Path.Combine(selection, "execution-all", "timing.tsv"), ct);
        timing.ShouldContain("\tfailed", StringComparison.Ordinal);
    }
}
