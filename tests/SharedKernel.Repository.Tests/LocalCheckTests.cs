using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Guards the <c>local-check</c> command: its lanes, its report of what could not run, its exit codes, and its refusal to pass having checked nothing.</summary>
public sealed class LocalCheckTests
{
    [Fact]
    public async Task All_lanes_passing_exits_zero_and_the_report_says_what_it_does_not_check()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        LocalCheckReport.Lane(result.Output, "whitespace").ShouldContain("passed", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "build").ShouldContain("passed", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "tests").ShouldContain("Core.Tests", StringComparison.Ordinal);
        result.Output.ShouldContain("This lane does not check:", StringComparison.Ordinal);
        foreach (var item in LocalCheck.NotCovered)
        {
            result.Output.ShouldContain(item, StringComparison.Ordinal);
        }

        result.Output.ShouldContain("this is not the whole of CI", StringComparison.Ordinal);
        result.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task The_lanes_run_the_socket_free_commands_with_node_reuse_off_and_the_test_host_directly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);
        var host = LocalCheckScript.FullPath(repository.Root, LocalCheckScript.TestHost);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check("--", "--filter-trait", "Category=Fast"), processes, static _ => throw new InvalidOperationException("No service starts."), ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldContain(command => command.StartsWith("dotnet format whitespace --folder " + LocalCheckScript.FullPath(repository.Root, "src/Core") + " --verify-no-changes --exclude bin/ obj/", StringComparison.Ordinal));
        processes.Commands.ShouldContain(command => command.StartsWith("dotnet restore ", StringComparison.Ordinal) && command.EndsWith(" --locked-mode -m:1 -nodeReuse:false", StringComparison.Ordinal));
        processes.Commands.ShouldContain(command => command.StartsWith("dotnet build ", StringComparison.Ordinal) && command.EndsWith(" -c Debug --no-restore -m:1 -nodeReuse:false", StringComparison.Ordinal));
        processes.Commands.ShouldContain(host + " --progress off --filter-trait Category=Fast");
        processes.Commands.ShouldNotContain(command => command.StartsWith("dotnet test", StringComparison.Ordinal), "dotnet test needs a socket.");
        processes.Commands.ShouldNotContain(command => command.Contains("--coverlet", StringComparison.Ordinal));
        var dotnet = processes.Calls.Where(static call => call.FileName == "dotnet" || call.FileName.EndsWith("Core.Tests", StringComparison.Ordinal)).ToArray();
        dotnet.ShouldNotBeEmpty();
        foreach (var call in dotnet)
        {
            call.Environment.ShouldNotBeNull();
            call.Environment["MSBUILDDISABLENODEREUSE"].ShouldBe("1");
            call.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"].ShouldBe("0");
        }
    }

    [Fact]
    public async Task Only_the_directories_of_the_changed_projects_are_checked_for_whitespace()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository) { Diff = "src/Dependent/Changed.cs\0" }.Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var folders = processes.Commands.Where(static command => command.StartsWith("dotnet format", StringComparison.Ordinal)).ToArray();
        folders.Length.ShouldBe(2);
        folders.ShouldContain(command => command.Contains(LocalCheckScript.FullPath(repository.Root, "src/Dependent"), StringComparison.Ordinal));
        folders.ShouldContain(command => command.Contains(LocalCheckScript.FullPath(repository.Root, "tests/Core.Tests"), StringComparison.Ordinal));
        folders.ShouldNotContain(command => command.Contains(LocalCheckScript.FullPath(repository.Root, "src/Core"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_working_tree_changes_select_projects_too()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository) { Diff = string.Empty }.Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        processes.Commands.ShouldContain("git diff --name-only --no-renames -z HEAD --");
        processes.Commands.ShouldContain("git ls-files -z --others --exclude-standard");
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("selected no project", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bad_whitespace_fails_the_lane_names_the_file_and_exits_two_while_the_other_lanes_still_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var bad = Path.Combine(repository.Root, "src/Core/Bad.cs");
        var script = new LocalCheckScript(repository)
        {
            Fail = (_, arguments) => arguments is ["format", "whitespace", "--folder", var folder, ..] && folder.EndsWith("Core", StringComparison.Ordinal)
                ? LocalCheckScript.Failure(2, bad + "(2,1): " + LocalCheckScript.Finding)
                : null,
        };
        var processes = new ProcessLog(script.Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        LocalCheckReport.Lane(result.Output, "whitespace").ShouldContain("FAILED", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "whitespace").ShouldContain("src/Core/Bad.cs", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "whitespace").ShouldContain("dotnet format whitespace --folder src/Core", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "build").ShouldContain("passed", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "tests").ShouldContain("passed", StringComparison.Ordinal);
        result.Output.ShouldContain("Result: FAILED.", StringComparison.Ordinal);
        result.Error.Trim().ShouldBe("local-check failed: whitespace.");
    }

    [Fact]
    public async Task Bad_whitespace_in_a_real_folder_is_found_by_dotnet_format()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.WriteFile(".editorconfig", "root = true\n[*.cs]\nindent_style = space\nindent_size = 4\nend_of_line = lf\ninsert_final_newline = true\n");
        repository.WriteFile("src/Core/Bad.cs", "class Bad\n{\n\tint x;   \n}\n");
        await GitCheckout.Init(repository.Root, ct);
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["local-check"], GitCheckout.RealGitAndFormat(processes), ct);

        // Assert
        result.ExitCode.ShouldBe(2, result.Error);
        LocalCheckReport.Lane(result.Output, "whitespace").ShouldContain("FAILED", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "whitespace").ShouldContain("src/Core/Bad.cs", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_tool_skips_the_lane_with_its_reason_and_strict_turns_the_skip_into_a_failure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var script = new LocalCheckScript(repository)
        {
            Fail = (_, arguments) => arguments is ["format", ..] ? "Failed to start dotnet: no absolute PATH directory contains it." : null,
        };

        // Act
        var lenient = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), new ProcessLog(script.Respond).Run, ct);
        var strict = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check("--strict"), new ProcessLog(script.Respond).Run, ct);

        // Assert
        lenient.ExitCode.ShouldBe(0, lenient.Error);
        LocalCheckReport.Lane(lenient.Output, "whitespace").ShouldContain("SKIPPED", StringComparison.Ordinal);
        LocalCheckReport.Lane(lenient.Output, "whitespace").ShouldContain("not installed", StringComparison.Ordinal);
        lenient.Output.ShouldContain("but some could not run", StringComparison.Ordinal);
        strict.ExitCode.ShouldBe(2);
        strict.Error.Trim().ShouldBe("local-check --strict failed because something could not run: whitespace.");
        LocalCheckReport.Lane(strict.Output, "whitespace").ShouldContain("SKIPPED", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_test_project_of_a_slice_with_a_service_is_listed_as_not_run_and_strict_fails_for_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.AddProject("tests/Host/Host.csproj", """<Project />""");
        repository.Configure("""
            [
              { "name": "library", "projects": [] },
              {
                "name": "hosted",
                "projects": ["tests/Core.Tests/Core.Tests.csproj"],
                "service": { "project": "tests/Host/Host.csproj", "readyFileVariable": "APP_READY_FILE", "valueVariable": "APP_VALUE" }
              }
            ]
            """);
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);

        // Act
        var lenient = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);
        var strict = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check("--strict"), new ProcessLog(new LocalCheckScript(repository).Respond).Run, ct);

        // Assert
        lenient.ExitCode.ShouldBe(0, lenient.Error);
        lenient.Output.ShouldContain("Selected but not run:", StringComparison.Ordinal);
        lenient.Output.ShouldContain("Core.Tests: slice hosted needs a service that `run` starts", StringComparison.Ordinal);
        LocalCheckReport.Lane(lenient.Output, "tests").ShouldContain("SKIPPED", StringComparison.Ordinal);
        processes.Commands.ShouldNotContain(command => command.Contains("--progress", StringComparison.Ordinal));
        strict.ExitCode.ShouldBe(2);
    }

    [Fact]
    public async Task A_test_assembly_that_the_build_did_not_produce_is_reported_as_not_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var script = new LocalCheckScript(repository);
        File.Delete(LocalCheckScript.FullPath(repository.Root, LocalCheckScript.TestAssembly));
        var processes = new ProcessLog(script.Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check("--strict"), processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Output.ShouldContain("Core.Tests: the built test assembly was not found at ", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "tests").ShouldContain("SKIPPED", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_socket_in_the_build_reports_the_sandbox_and_the_doctor_instead_of_the_stack_trace()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var script = new LocalCheckScript(repository)
        {
            Fail = (_, arguments) => arguments is ["build", ..]
                ? LocalCheckScript.Failure(1, "Unhandled exception. System.Net.Sockets.SocketException (13): Permission denied\n   at System.Net.Sockets.Socket.Bind")
                : null,
        };
        var processes = new ProcessLog(script.Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        LocalCheckReport.Lane(result.Output, "build").ShouldContain("The sandbox blocks Unix domain sockets", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "build").ShouldContain("sharedkernel-sandbox doctor", StringComparison.Ordinal);
        result.Output.ShouldNotContain("Socket.Bind", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "tests").ShouldContain("SKIPPED", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "tests").ShouldContain("build lane did not pass", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("System.Net.Sockets.SocketException (13): Permission denied", true)]
    [InlineData("connect to the pipe: Permission denied", true)]
    [InlineData("Unix socket Permission denied", true)]
    [InlineData("error: Permission denied reading /etc/shadow", false)]
    [InlineData("System.Net.Sockets.SocketException (111): Connection refused", false)]
    [InlineData("System.Net.Sockets.SocketException (1): Operation not permitted", true)]
    [InlineData("/home/me/.nuget/Pipelines/Sockets.Unofficial.dll: Permission denied", false)]
    [InlineData("Permission denied: /home/me/.nuget/Pipelines/Sockets.Unofficial.dll", false)]
    [InlineData("error CS0246: type not found", false)]
    public void Socket_refusal_is_recognized_by_the_exception_or_by_permission_denied_next_to_a_socket(string output, bool refused) =>
        SocketRefusal.IsRefused(output).ShouldBe(refused);

    [Fact]
    public async Task A_failing_test_project_fails_the_tests_lane_and_exit_zero_tests_say_nothing_was_checked()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var host = LocalCheckScript.FullPath(repository.Root, LocalCheckScript.TestHost);
        var script = new LocalCheckScript(repository)
        {
            Fail = (fileName, _) => fileName == host ? LocalCheckScript.Failure(8, "Zero tests ran") : null,
        };

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), new ProcessLog(script.Respond).Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        LocalCheckReport.Lane(result.Output, "tests").ShouldContain("FAILED", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "tests").ShouldContain("no test ran (exit code 8)", StringComparison.Ordinal);
        result.Error.Trim().ShouldBe("local-check failed: tests.");
    }

    [Fact]
    public async Task A_test_assembly_without_a_host_runs_through_dotnet()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var script = new LocalCheckScript(repository);
        File.Delete(LocalCheckScript.FullPath(repository.Root, LocalCheckScript.TestHost));
        var processes = new ProcessLog(script.Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        processes.Commands.ShouldContain("dotnet " + LocalCheckScript.FullPath(repository.Root, LocalCheckScript.TestAssembly) + " --progress off");
    }

    [Fact]
    public async Task A_change_that_selects_no_project_fails_because_it_would_check_nothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository) { Diff = "docs/guide.md\0" }.Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("would check nothing", StringComparison.Ordinal);
        processes.Commands.ShouldNotContain(static command => command.StartsWith("dotnet", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Outside_a_git_work_tree_the_command_fails_before_it_runs_a_lane()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository) { InsideWorkTree = false }.Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("needs a Git work tree", StringComparison.Ordinal);
        processes.Commands.ShouldNotContain(static command => command.StartsWith("dotnet", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_base_that_is_an_option_or_not_a_commit_is_refused()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var script = new LocalCheckScript(repository)
        {
            Fail = (_, arguments) => arguments is ["rev-parse", "--verify", ..] ? LocalCheckScript.Failure(1, string.Empty) : null,
        };

        // Act
        var option = await RepositoryToolInvocation.Run(repository.Root, ["local-check", "--base", "--upload-pack=x"], new ProcessLog(script.Respond).Run, ct);
        var missing = await RepositoryToolInvocation.Run(repository.Root, ["local-check", "--base", "nope"], new ProcessLog(script.Respond).Run, ct);

        // Assert
        option.ExitCode.ShouldNotBe(0);
        missing.ExitCode.ShouldBe(2);
        missing.Error.ShouldContain("--base nope is not a commit", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_base_every_test_project_is_selected()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["local-check"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("no --base revision", StringComparison.Ordinal);
        processes.Commands.ShouldNotContain(static command => command.StartsWith("git diff", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Each_lane_is_timed_with_the_injected_clock_and_logged_in_the_selection_directory()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);
        var selection = Path.Combine(repository.Root, "TestResults", "local-check");
        using var output = new StringWriter();

        // Act
        await LocalCheck.Run(
            repository.Root,
            selection,
            new LocalCheckOptions(null, "Debug", false, [], TimeSpan.FromMinutes(5)),
            processes.Run,
            processes.RunConfigured,
            output,
            TextWriter.Null,
            ct,
            new SteppingTimeProvider());

        // Assert
        LocalCheckReport.Lane(output.ToString(), "whitespace").ShouldContain("(2.0s)", StringComparison.Ordinal);
        LocalCheckReport.Lane(output.ToString(), "build").ShouldContain("(2.0s)", StringComparison.Ordinal);
        var log = await File.ReadAllTextAsync(Path.Combine(selection, "local-check-build.log"), ct);
        log.ShouldContain("restore --locked-mode and build", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_output_in_the_report_is_bounded_and_escaped()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var noisy = string.Join('\n', Enumerable.Range(0, 100).Select(static index => "line " + index + " \u001b[31mred"));
        var script = new LocalCheckScript(repository)
        {
            Fail = (_, arguments) => arguments is ["build", ..] ? LocalCheckScript.Failure(1, noisy) : null,
        };

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), new ProcessLog(script.Respond).Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        var build = LocalCheckReport.Lane(result.Output, "build");
        build.ShouldContain("line 99", StringComparison.Ordinal);
        build.ShouldNotContain("line 10 ", StringComparison.Ordinal);
        build.ShouldNotContain('\u001b'.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_lane_passing_fails_even_without_strict_and_the_report_says_nothing_was_checked()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var script = new LocalCheckScript(repository)
        {
            Fail = (fileName, arguments) => fileName == "dotnet" ? "Failed to start dotnet: no absolute PATH directory contains it." : null,
        };

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), new ProcessLog(script.Respond).Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        LocalCheckReport.Lane(result.Output, "whitespace").ShouldContain("SKIPPED", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "build").ShouldContain("SKIPPED", StringComparison.Ordinal);
        LocalCheckReport.Lane(result.Output, "tests").ShouldContain("SKIPPED", StringComparison.Ordinal);
        result.Output.ShouldContain("No lane passed, so nothing was checked.", StringComparison.Ordinal);
        result.Output.ShouldNotContain("the lanes that ran passed", StringComparison.Ordinal);
        result.Error.Trim().ShouldBe("local-check checked nothing: no lane passed.");
    }

    [Theory]
    [InlineData("""[{ "name": "hosted", "projects": ["tests/Core.Tests/Core.Tests.csproj"], "openApi": ["docs"] }]""", "generated OpenAPI documents")]
    [InlineData("""[{ "name": "hosted", "projects": ["tests/Core.Tests/Core.Tests.csproj"], "playwright": { "project": "tests/Core.Tests/Core.Tests.csproj", "browsers": ["chromium"] } }]""", "Playwright browsers")]
    public async Task A_test_project_of_a_slice_that_needs_openapi_documents_or_browsers_is_not_run(string slices, string needs)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.Configure(slices, defaultSlice: null);
        var configured = await File.ReadAllTextAsync(Path.Combine(repository.Root, ".config/sharedkernel-ci.json"), ct);
        repository.WriteFile(
            ".config/sharedkernel-ci.json",
            configured.TrimEnd().TrimEnd('}')
                + ",\n  \"openApi\": [{ \"name\": \"docs\", \"project\": \"src/Core/Core.csproj\", \"generateProperty\": \"Gen\", \"refreshProperty\": \"Ref\" }]\n}\n");
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        result.Output.ShouldContain("Core.Tests: slice hosted needs " + needs + ", which `run` provides.", StringComparison.Ordinal);
        processes.Commands.ShouldNotContain(command => command.Contains("--progress", StringComparison.Ordinal));
        result.ExitCode.ShouldBe(0, result.Error);
    }

    [Fact]
    public async Task A_child_process_past_the_timeout_fails_its_lane_as_timed_out()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var script = new LocalCheckScript(repository);
        var processes = new ProcessLog(async (fileName, arguments, token) =>
        {
            if (arguments is ["build", ..])
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }

            return await script.Respond(fileName, arguments, token);
        });
        var selection = Path.Combine(repository.Root, "TestResults", "local-check");

        // Act
        var run = () => LocalCheck.Run(
            repository.Root,
            selection,
            new LocalCheckOptions(null, "Debug", false, [], TimeSpan.FromMilliseconds(200)),
            processes.Run,
            processes.RunConfigured,
            TextWriter.Null,
            TextWriter.Null,
            ct);
        var thrown = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        thrown.Message.ShouldBe("local-check failed: build.");
        var log = await File.ReadAllTextAsync(Path.Combine(selection, "local-check-build.log"), ct);
        log.ShouldContain("timed out after 0.2 seconds", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_test_project_with_an_empty_target_path_is_reported_as_multi_target_and_not_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var script = new LocalCheckScript(repository)
        {
            Evaluation = string.Empty,
        };

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check("--strict"), new ProcessLog(script.Respond).Run, ct);

        // Assert
        result.Output.ShouldContain("Core.Tests: multi-target project: not supported, set a single target", StringComparison.Ordinal);
        result.Output.ShouldNotContain("was not found at", StringComparison.Ordinal);
        result.ExitCode.ShouldBe(2);
    }

    [Theory]
    [InlineData("untracked", "", "src/Core/New.cs\0")]
    [InlineData("tracked", "src/Core/Changed.cs\0", "")]
    public async Task An_untracked_or_a_tracked_modified_file_of_the_working_tree_selects_and_runs_the_tests(string kind, string workingDiff, string untracked)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var script = new LocalCheckScript(repository) { Diff = string.Empty, WorkingDiff = workingDiff, Untracked = untracked };
        var processes = new ProcessLog(script.Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check(), processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0, kind + ": " + result.Error);
        result.Output.ShouldContain(".cs: owned by src/Core/Core.csproj.", StringComparison.Ordinal);
        processes.Commands.ShouldContain(command => command.EndsWith(Path.GetFileName(LocalCheckScript.TestHost) + " --progress off", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("modified")]
    [InlineData("untracked")]
    public async Task In_a_real_checkout_a_modified_or_an_untracked_file_selects_the_tests_that_the_commit_alone_does_not(string kind)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.WriteFile(".gitignore", "TestResults/\nbin/\n");
        repository.WriteFile("src/Core/Existing.cs", "class Existing\n{\n}\n");
        await GitCheckout.Init(repository.Root, ct);
        await GitCheckout.CommitAll(repository.Root, ct);
        repository.WriteFile(kind == "modified" ? "src/Core/Existing.cs" : "src/Core/New.cs", "class Changed\n{\n}\n");
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["local-check", "--base", "HEAD"], GitCheckout.RealGit(processes), ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain(kind == "modified" ? "src/Core/Existing.cs: owned by" : "src/Core/New.cs: owned by", StringComparison.Ordinal);
        processes.Commands.ShouldContain(command => command.EndsWith(Path.GetFileName(LocalCheckScript.TestHost) + " --progress off", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("1441", false)]
    [InlineData("1", true)]
    [InlineData("1440", true)]
    public async Task The_timeout_option_accepts_one_to_1440_minutes(string minutes, bool accepted)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(new LocalCheckScript(repository).Respond);

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, LocalCheckReport.Check("--timeout-minutes", minutes), processes.Run, ct);

        // Assert
        if (accepted)
        {
            result.ExitCode.ShouldBe(0, result.Error);
        }
        else
        {
            result.ExitCode.ShouldBe(1);
            result.Error.ShouldContain("--timeout-minutes requires 1 to 1440.", StringComparison.Ordinal);
        }
    }
}
