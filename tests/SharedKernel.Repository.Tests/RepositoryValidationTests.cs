using System.Text.Json;

namespace PandaBear.Repository.Tests;

public sealed class RepositoryValidationTests
{
    private const string CoreTests = "tests/Core.Tests/Core.Tests.csproj";
    private const string HostedTests = "tests/Hosted/Hosted.csproj";
    private const string HostedSlices = """
        [
          { "name": "library", "projects": [] },
          { "name": "hosted", "projects": ["tests/Hosted/Hosted.csproj"] },
          { "name": "idle", "projects": ["tests/Idle/Idle.csproj"] }
        ]
        """;

    [Fact]
    public async Task A_slice_restores_builds_and_tests_one_generated_filter_without_capping_test_parallelism()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        var execution = Path.Combine(selection, "execution-library");
        var filter = Path.Combine(execution, "tests.slnf");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            Path.GetDirectoryName(scope.Root)!, ["run", "--slice", "library", "--root", scope.Root], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        processes.Commands.ShouldBe(
        [
            "git rev-parse HEAD",
            "dotnet restore " + filter + " --locked-mode",
            "dotnet build " + filter + " -c Release --no-restore",
            "dotnet test --solution " + filter + " -c Release --no-build --no-restore --results-directory " + Path.Combine(execution, "results"),
        ]);
        processes.Calls.ShouldAllSatisfy(call => call.WorkingDirectory.ShouldBe(scope.Root));
        var projects = await SelectionTestFiles.Projects(Path.GetDirectoryName(filter)!, "tests", ct);
        projects.ShouldBe(["tests/Core.Tests/Core.Tests.csproj"]);
    }

    [Fact]
    public async Task Several_slices_merge_into_one_filter_and_one_test_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        scope.Configure(HostedSlices);
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]), ("hosted", [HostedTests]));
        var filter = Path.Combine(selection, "execution-library+hosted", "tests.slnf");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library", "--slice", "hosted"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        processes.Commands.Count(static command => command.StartsWith("dotnet test --solution ", StringComparison.Ordinal)).ShouldBe(1);
        var projects = await SelectionTestFiles.Projects(Path.GetDirectoryName(filter)!, "tests", ct);
        projects.ShouldBe(["tests/Core.Tests/Core.Tests.csproj", "tests/Hosted/Hosted.csproj"]);
    }

    [Fact]
    public async Task Without_a_slice_every_required_slice_runs_in_one_filter()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        scope.Configure(HostedSlices);
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]), ("hosted", [HostedTests]), ("idle", []));
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--configuration", "Debug"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var filter = Path.Combine(selection, "execution", "tests.slnf");
        var projects = await SelectionTestFiles.Projects(Path.GetDirectoryName(filter)!, "tests", ct);
        projects.ShouldBe(["tests/Core.Tests/Core.Tests.csproj", "tests/Hosted/Hosted.csproj"]);
        processes.Commands.ShouldContain("dotnet build " + filter + " -c Debug --no-restore");
    }

    [Fact]
    public async Task Run_writes_the_dotnet_output_to_standard_error()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection("fixture", ("library", [CoreTests]));
        var processes = new ProcessLog(static (fileName, arguments, _) =>
            Task.FromResult(fileName == "git" ? "fixture" : "dotnet " + arguments[0] + " output" + Environment.NewLine));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, TestContext.Current.CancellationToken);

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
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe("No test projects selected for library" + Environment.NewLine);
        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
    }

    [Fact]
    public async Task Stale_selection_cannot_run()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection("previous", ("library", [CoreTests]));
        var processes = new ProcessLog(static (fileName, _, _) => Task.FromResult(fileName == "git" ? "current" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
        result.Error.ShouldContain("revision", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../outside/Outside.csproj")]
    [InlineData("/outside/Outside.csproj")]
    [InlineData("tests\\Core.Tests\\Core.Tests.csproj")]
    public async Task A_plan_that_names_a_project_outside_the_checkout_cannot_run(string project)
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection("fixture", ("library", [project]));
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("selection.json names a test project outside the checkout", StringComparison.Ordinal);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
    }

    [Fact]
    public async Task A_failed_build_does_not_run_tests_and_records_the_phases_the_manifest_and_diagnostics()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        var processes = new ProcessLog(static (fileName, arguments, _) => fileName == "git"
            ? Task.FromResult("fixture")
            : arguments[0] == "build" ? throw new InvalidOperationException("Build failed.") : Task.FromResult("restored"));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Build failed.", StringComparison.Ordinal);
        processes.Commands.Any(static command => command.StartsWith("dotnet test", StringComparison.Ordinal)).ShouldBeFalse();
        var execution = Path.Combine(selection, "execution-library");
        var timings = (await File.ReadAllLinesAsync(Path.Combine(execution, "phase-timings.tsv"), ct)).Select(static line => line.Split('\t')).ToArray();
        timings[0].ShouldBe(["phase", "started_at_utc", "duration_seconds", "status"]);
        timings[1..].Select(static line => line[0] + " " + line[3]).ShouldBe(["load passed", "restore passed", "build failed", "total failed"]);
        var restoreLog = await File.ReadAllTextAsync(Path.Combine(execution, "restore.log"), ct);
        restoreLog.ShouldBe("restored");
        var buildLog = await File.ReadAllTextAsync(Path.Combine(execution, "build.log"), ct);
        buildLog.ShouldContain("Build failed.", StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(execution, "manifest.json"), ct));
        manifest.RootElement.GetProperty("revision").GetString().ShouldBe("fixture");
        manifest.RootElement.GetProperty("status").GetString().ShouldBe("failed");
        manifest.RootElement.GetProperty("slices").EnumerateArray().Select(static slice => slice.GetString()).ShouldBe(["library"]);
        manifest.RootElement.GetProperty("projects").EnumerateArray().Select(static project => project.GetString()).ShouldBe([CoreTests]);
        manifest.RootElement.GetProperty("phases").EnumerateArray().Select(static phase => phase.GetProperty("name").GetString()).ShouldBe(["load", "restore", "build"]);
        var diagnostics = await File.ReadAllTextAsync(Path.Combine(execution, "diagnostics.txt"), ct);
        diagnostics.ShouldContain("Status: failed", StringComparison.Ordinal);
        diagnostics.ShouldContain("Build failed.", StringComparison.Ordinal);
        diagnostics.ShouldContain("build.log", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_invalid_configuration_still_records_the_failed_load()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        scope.WriteFile(".config/sharedkernel-ci.json", "{}");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(".config/sharedkernel-ci.json", StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
        var execution = Path.Combine(selection, "execution-library");
        var timings = await File.ReadAllTextAsync(Path.Combine(execution, "phase-timings.tsv"), ct);
        timings.ShouldContain("load\t", StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(execution, "manifest.json"), ct));
        manifest.RootElement.GetProperty("status").GetString().ShouldBe("failed");
        manifest.RootElement.GetProperty("revision").GetString().ShouldBe("unavailable");
        var diagnostics = await File.ReadAllTextAsync(Path.Combine(execution, "diagnostics.txt"), ct);
        diagnostics.ShouldContain(".config/sharedkernel-ci.json", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_slice_fails_with_the_domain_exit_code_before_building()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection("fixture", ("library", [CoreTests]));
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "other"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Unknown slice: other.", StringComparison.Ordinal);
        processes.Calls.ShouldAllSatisfy(call => call.FileName.ShouldBe("git"));
    }

    [Fact]
    public async Task Run_reads_the_selection_directory_under_the_root_and_appends_the_phases_to_the_github_summary()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelectionIn("plans/current", "fixture", ("library", [CoreTests]));
        scope.WriteFile("summary.md", "previous\n");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["run", "--slice", "library", "--selection", "plans/current", "--github-summary", "summary.md"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(selection, "execution-library", "manifest.json"), ct));
        manifest.RootElement.GetProperty("status").GetString().ShouldBe("passed");
        var summary = await File.ReadAllTextAsync(Path.Combine(scope.Root, "summary.md"), ct);
        summary.ShouldStartWith("previous\n## sharedkernel-ci run library: passed\n\n| Phase | Status | Seconds |\n| --- | --- | --- |\n| load | passed | ", StringComparison.Ordinal);
        summary.ShouldContain("| test | passed | ", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coverage_collects_cobertura_per_module_and_fails_when_a_selected_project_produced_none()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests, HostedTests]));
        var results = Path.Combine(selection, "execution-library", "results");
        var processes = new ProcessLog(async (fileName, arguments, token) =>
        {
            if (arguments is ["test", ..])
            {
                Directory.CreateDirectory(Path.Combine(results, "Core.Tests", "net10.0_x64"));
                await File.WriteAllTextAsync(Path.Combine(results, "Core.Tests", "net10.0_x64", "coverage.cobertura.1.xml"), "<coverage />", token);
            }

            return fileName == "git" ? "fixture" : string.Empty;
        });

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library", "--coverage"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        processes.Commands.ShouldContain(
            "dotnet test --solution " + Path.Combine(selection, "execution-library", "tests.slnf") + " -c Release --no-build --no-restore --results-directory " + results
                + " --results-directory-layout per-module -- --coverlet --coverlet-output-format cobertura");
        result.Error.ShouldContain("No Cobertura report for tests/Hosted/Hosted.csproj", StringComparison.Ordinal);
        result.Error.ShouldNotContain("Core.Tests.csproj", StringComparison.Ordinal);
        var timings = await File.ReadAllTextAsync(Path.Combine(selection, "execution-library", "phase-timings.tsv"), ct);
        timings.ShouldContain("coverage\t", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Excluded_traits_reach_the_test_run_as_filter_not_trait_arguments()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        var results = Path.Combine(selection, "execution-library", "results");
        var processes = new ProcessLog(static (fileName, _, _) => Task.FromResult(fileName == "git" ? "fixture" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root,
            ["run", "--slice", "library", "--exclude-trait", "Requires=external-engine", "--exclude-trait", "Scope=slow"],
            processes.Run,
            ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldContain(
            "dotnet test --solution " + Path.Combine(selection, "execution-library", "tests.slnf") + " -c Release --no-build --no-restore --results-directory " + results
                + " -- --filter-not-trait Requires=external-engine --filter-not-trait Scope=slow");
    }

    [Fact]
    public async Task Excluded_traits_follow_the_coverage_arguments_after_one_separator()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        var results = Path.Combine(selection, "execution-library", "results");
        var processes = new ProcessLog(async (fileName, arguments, token) =>
        {
            if (arguments is ["test", ..])
            {
                Directory.CreateDirectory(Path.Combine(results, "Core.Tests", "net10.0_x64"));
                await File.WriteAllTextAsync(Path.Combine(results, "Core.Tests", "net10.0_x64", "coverage.cobertura.1.xml"), "<coverage />", token);
            }

            return fileName == "git" ? "fixture" : string.Empty;
        });

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["run", "--slice", "library", "--coverage", "--exclude-trait", "Requires=external-engine"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldContain(
            "dotnet test --solution " + Path.Combine(selection, "execution-library", "tests.slnf") + " -c Release --no-build --no-restore --results-directory " + results
                + " --results-directory-layout per-module -- --coverlet --coverlet-output-format cobertura --filter-not-trait Requires=external-engine");
    }

    [Theory]
    [InlineData("Requires")]
    [InlineData("=external-engine")]
    [InlineData("Requires=")]
    [InlineData("--Requires=x")]
    [InlineData("Requires=a b")]
    public async Task Excluded_trait_must_be_a_name_and_value_pair_before_running_processes(string trait)
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteSelection("fixture", ("library", [CoreTests]));
        var processes = new ProcessLog(static (fileName, _, _) => Task.FromResult(fileName == "git" ? "fixture" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["run", "--slice", "library", "--exclude-trait", trait], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("--exclude-trait requires name=value", StringComparison.Ordinal);
        processes.Commands.ShouldNotContain(static command => command.StartsWith("dotnet test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenApi_targets_of_the_slice_generate_after_the_build_and_playwright_installs_before_the_tests()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration
            .Replace(
                "\"version\": 1,",
                "\"version\": 1,\n  \"openApi\": [{ \"name\": \"admin\", \"project\": \"src/Dependent/Dependent.csproj\", \"generateProperty\": \"GenerateAdmin\", \"refreshProperty\": \"RefreshAdmin\" }],",
                StringComparison.Ordinal)
            .Replace(
                """{ "name": "library", "projects": [] }""",
                """{ "name": "library", "projects": ["tests/Core.Tests/Core.Tests.csproj"], "openApi": ["admin"], "playwright": { "project": "tests/Core.Tests/Core.Tests.csproj", "browsers": ["chromium"] } }""",
                StringComparison.Ordinal));
        scope.WriteFile("tests/Core.Tests/bin/Release/net10.0/playwright.ps1", "# installer");
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        var processes = new ProcessLog(static (fileName, arguments, _) => Task.FromResult(
            fileName == "git" ? "fixture" : arguments is ["msbuild", ..] ? "bin\\Release\\net10.0\\" + Environment.NewLine : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["run", "--slice", "library", "--install-playwright", "--playwright-dependencies"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var commands = processes.Calls.Select(static call => Path.GetFileNameWithoutExtension(call.FileName) + " " + string.Join(' ', call.Arguments)).ToArray();
        commands[3..^1].ShouldBe(
        [
            "dotnet build --no-restore src/Dependent/Dependent.csproj -c Release -p:GenerateAdmin=true",
            "dotnet msbuild " + Path.GetFullPath(Path.Combine(scope.Root, CoreTests)) + " -getProperty:OutputPath -p:Configuration=Release",
            "pwsh " + Path.Combine(scope.Root, "tests", "Core.Tests", "bin", "Release", "net10.0", "playwright.ps1") + " install --with-deps chromium",
        ]);
        Path.IsPathFullyQualified(processes.Calls[3].FileName).ShouldBeTrue("OpenAPI generation runs the dotnet host that runs the tool.");
        commands[^1].ShouldStartWith("dotnet test --solution ", StringComparison.Ordinal);
        var timings = await File.ReadAllTextAsync(Path.Combine(selection, "execution-library", "phase-timings.tsv"), ct);
        timings.ShouldContain("openapi-admin\t", StringComparison.Ordinal);
        timings.ShouldContain("playwright\t", StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_target_projects_join_the_filter_so_generation_finds_them_restored_and_built()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        scope.AddProject("src/Api/Api.csproj", """<Project />""");
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration
            .Replace(
                "\"version\": 1,",
                "\"version\": 1,\n  \"openApi\": [{ \"name\": \"api\", \"project\": \"src/Api/Api.csproj\", \"generateProperty\": \"GenerateApi\", \"refreshProperty\": \"RefreshApi\" }],",
                StringComparison.Ordinal)
            .Replace(
                """{ "name": "library", "projects": [] }""",
                """{ "name": "library", "projects": ["tests/Core.Tests/Core.Tests.csproj"], "openApi": ["api"] }""",
                StringComparison.Ordinal));
        var selection = scope.WriteSelection("fixture", ("library", [CoreTests]));
        var processes = new ProcessLog(static (fileName, _, _) => Task.FromResult(fileName == "git" ? "fixture" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var projects = await SelectionTestFiles.Projects(Path.Combine(selection, "execution-library"), "tests", ct);
        projects.ShouldBe(["src/Api/Api.csproj", CoreTests], "Generation builds the target with --no-restore, so the filter must restore and build it.");
    }

    [Fact]
    public async Task Playwright_installs_only_when_requested()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        scope.WriteFile(".config/sharedkernel-ci.json", RepositoryTestScope.DefaultConfiguration.Replace(
            """{ "name": "library", "projects": [] }""",
            """{ "name": "library", "projects": ["tests/Core.Tests/Core.Tests.csproj"], "playwright": { "project": "tests/Core.Tests/Core.Tests.csproj", "browsers": ["chromium"] } }""",
            StringComparison.Ordinal));
        scope.WriteSelection("fixture", ("library", [CoreTests]));
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        processes.Calls.ShouldAllSatisfy(static call => call.FileName.ShouldNotBe("pwsh"));
        processes.Calls.ShouldAllSatisfy(static call => call.Arguments[0].ShouldNotBe("msbuild"));
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
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldAllSatisfy(call => call.Token.IsCancellationRequested.ShouldBeTrue());
        processes.Commands.Any(static command => command.StartsWith("dotnet test", StringComparison.Ordinal)).ShouldBeFalse();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(selection, "execution-library", "manifest.json"), ct));
        manifest.RootElement.GetProperty("status").GetString().ShouldBe("failed");
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
        var result = await RepositoryToolInvocation.Run(scope.Root, ["run", "--slice", "library"], processes.Run, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
        processes.Commands.Any(static command => command.StartsWith("dotnet test", StringComparison.Ordinal)).ShouldBeFalse();
        var timing = await File.ReadAllTextAsync(Path.Combine(selection, "execution-library", "phase-timings.tsv"), ct);
        timing.ShouldContain("\tfailed", StringComparison.Ordinal);
    }
}
