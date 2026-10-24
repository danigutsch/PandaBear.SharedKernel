using PandaBear.CI.Tool;
using PandaBear.Repository.Tests.Conventions;

namespace PandaBear.Repository.Tests;

/// <summary>Guards the <c>select</c> command: its Git comparison, its plan files, and its GitHub Actions outputs.</summary>
public static class SelectionCommandTests
{
    private const string BaseRevision = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public static async Task An_empty_diff_writes_the_pinned_outputs_and_no_filter()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var githubOutput = Path.Combine(repository.Root, "github-output.txt");
        await File.WriteAllTextAsync(githubOutput, "existing=value\n", ct);

        // Act
        var result = await RepositoryToolInvocation.Run(
            repository.Root, ["select", "--base", BaseRevision, "--github-output", "github-output.txt"], new ProcessLog().Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe("No path changed between the base and HEAD." + Environment.NewLine);
        var outputs = await File.ReadAllTextAsync(githubOutput, ct);
        outputs.ShouldBe("existing=value\nbuild_required=false\ndocumentation_only=true\nmatrix=[]\nlibrary_required=false\n");
        var selection = Path.Combine(repository.Root, "TestResults", "selection");
        string?[] files = [.. Directory.EnumerateFiles(selection).Select(Path.GetFileName)];
        files.ShouldBe(["selection.json"]);
        var plan = await SelectionTestFiles.ReadPlan(selection, ct);
        plan.Revision.ShouldBe("fixture");
        plan.ComparisonBase.ShouldBe(BaseRevision);
        plan.FullValidation.ShouldBeFalse();
        plan.BuildRequired.ShouldBeFalse();
        plan.DocumentationOnly.ShouldBeTrue();
        plan.BuildProjects.ShouldBeEmpty();
        var slice = plan.Slices.ShouldHaveSingleItem();
        slice.Name.ShouldBe("library");
        slice.Required.ShouldBeFalse();
        slice.Tests.ShouldBeEmpty();
    }

    [Fact]
    public static async Task A_selective_change_writes_the_pinned_slice_and_build_filters()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.AddTestProject("tests/Unrelated.Tests/Unrelated.Tests.csproj");
        repository.AddTestProject("tests/Hosted.Tests/Hosted.Tests.csproj");
        repository.Configure("""
            [
              { "name": "library", "projects": [] },
              { "name": "hosted", "projects": ["tests/Hosted.Tests/Hosted.Tests.csproj"] }
            ]
            """);
        var processes = new ProcessLog(SelectionTestFiles.Git(diff: "src/Core/Changed.cs\0"));
        var githubOutput = Path.Combine(repository.Root, "github-output.txt");

        // Act
        var result = await RepositoryToolInvocation.Run(
            repository.Root, ["select", "--base", BaseRevision, "--github-output", githubOutput], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var selection = Path.Combine(repository.Root, "TestResults", "selection");
        var library = await File.ReadAllTextAsync(Path.Combine(selection, "library.slnf"), ct);
        var build = await File.ReadAllTextAsync(Path.Combine(selection, "build.slnf"), ct);
        var outputs = await File.ReadAllTextAsync(githubOutput, ct);
        library.ShouldBe("""
            {
              "solution": {
                "path": "../../SharedKernel.slnx",
                "projects": [
                  "tests/Core.Tests/Core.Tests.csproj"
                ]
              }
            }

            """.ReplaceLineEndings("\n"));
        build.ShouldBe("""
            {
              "solution": {
                "path": "../../SharedKernel.slnx",
                "projects": [
                  "src/Core/Core.csproj",
                  "src/Dependent/Dependent.csproj"
                ]
              }
            }

            """.ReplaceLineEndings("\n"));
        File.Exists(Path.Combine(selection, "hosted.slnf")).ShouldBeFalse();
        outputs.ShouldBe(
            "build_required=true\ndocumentation_only=false\nmatrix=[\"library\"]\nlibrary_required=true\nhosted_required=false\n");
        var plan = await SelectionTestFiles.ReadPlan(selection, ct);
        plan.Slices[0].Tests.ShouldHaveSingleItem().SelectedBy.ShouldBe(["src/Core/Changed.cs"]);
        result.Output.ShouldContain("src/Core/Changed.cs: owned by src/Core/Core.csproj.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Full_validation_writes_complete_filters_for_every_slice()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.AddTestProject("tests/Hosted.Tests/Hosted.Tests.csproj");
        repository.Configure("""
            [
              { "name": "library", "projects": [] },
              { "name": "hosted", "projects": ["tests/Hosted.Tests/Hosted.Tests.csproj"] }
            ]
            """);
        var githubOutput = Path.Combine(repository.Root, "github-output.txt");

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--github-output", githubOutput], new ProcessLog().Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe("Full validation: no --base revision." + Environment.NewLine);
        var selection = Path.Combine(repository.Root, "TestResults", "selection");
        var library = await SelectionTestFiles.Projects(selection, "library", ct);
        var hosted = await SelectionTestFiles.Projects(selection, "hosted", ct);
        var build = await SelectionTestFiles.Projects(selection, "build", ct);
        var outputs = await File.ReadAllTextAsync(githubOutput, ct);
        var plan = await SelectionTestFiles.ReadPlan(selection, ct);
        library.ShouldBe(["tests/Core.Tests/Core.Tests.csproj"]);
        hosted.ShouldBe(["tests/Hosted.Tests/Hosted.Tests.csproj"]);
        build.ShouldBe(["src/Core/Core.csproj", "src/Dependent/Dependent.csproj", "tests/Core.Tests/Core.Tests.csproj", "tests/Hosted.Tests/Hosted.Tests.csproj"]);
        outputs.ShouldBe("build_required=true\ndocumentation_only=false\nmatrix=[\"library\",\"hosted\"]\nlibrary_required=true\nhosted_required=true\n");
        plan.FullValidation.ShouldBeTrue();
    }

    [Fact]
    public static async Task Selection_compares_the_base_with_head_in_the_repository_root()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(SelectionTestFiles.Git(diff: "src/Core/Changed.cs\0"));

        // Act
        var result = await RepositoryToolInvocation.Run(Path.GetTempPath(), ["select", "--base", BaseRevision, "--root", repository.Root], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        processes.Commands.ShouldBe(
        [
            "git merge-base --is-ancestor " + BaseRevision + " HEAD",
            "git diff --name-only --no-renames -z " + BaseRevision + "...HEAD --",
            "git rev-parse HEAD",
        ]);
        processes.Calls.ShouldAllSatisfy(call => call.WorkingDirectory.ShouldBe(repository.Root));
        result.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public static async Task Documentation_changes_list_the_tracked_build_files_to_check_that_they_are_inert()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.WriteFile("src/Core/Core.csproj", """<Project><Target Name="T"><Exec Command="true" /></Target></Project>""");
        var processes = new ProcessLog(SelectionTestFiles.Git(diff: "docs/guide.md\0", files: "src/Core/Core.csproj\0src/Dependent/Dependent.csproj\0"));

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", BaseRevision], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        processes.Commands.ShouldContain("git ls-files -z -- *.csproj *.props *.targets");
        result.Output.ShouldBe(
            "Full validation: docs/guide.md: documentation patterns apply only while every tracked build file is inert, and src/Core/Core.csproj uses Exec."
                + Environment.NewLine);
    }

    [Theory]
    [InlineData("", "Full validation: no --base revision.")]
    [InlineData("abc123", "Full validation: --base is not a full hexadecimal Git object ID.")]
    [InlineData("-p", "Full validation: --base is not a full hexadecimal Git object ID.")]
    [InlineData("--output=changes.txt", "Full validation: --base is not a full hexadecimal Git object ID.")]
    public static async Task An_empty_or_invalid_base_selects_full_validation_without_comparing(string revision, string reason)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", revision], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe(reason + Environment.NewLine);
        processes.Commands.ShouldBe(["git rev-parse HEAD"]);
        File.Exists(Path.Combine(repository.Root, "changes.txt")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("merge-base", "Full validation: --base " + BaseRevision + " is not an ancestor of HEAD in this checkout.")]
    [InlineData("diff", "Full validation: git diff " + BaseRevision + "...HEAD failed or did not finish within 30 seconds.")]
    public static async Task A_base_that_is_not_an_ancestor_or_a_failed_diff_selects_full_validation(string failing, string reason)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog((_, arguments, _) => arguments[0] == failing
            ? throw new InvalidOperationException("Command failed (1): git " + failing)
            : Task.FromResult(arguments[0] == "rev-parse" ? "fixture" : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", BaseRevision], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var plan = await SelectionTestFiles.ReadPlan(Path.Combine(repository.Root, "TestResults", "selection"), ct);
        result.Output.ShouldBe(reason + Environment.NewLine);
        plan.FullValidation.ShouldBeTrue();
    }

    [Fact]
    public static async Task A_git_command_that_runs_longer_than_thirty_seconds_selects_full_validation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var time = new ImmediateTimeProvider(TimeSpan.FromSeconds(30));
        var processes = new ProcessLog(async (_, arguments, token) =>
        {
            if (arguments[0] == "diff")
            {
                await Task.Delay(Timeout.Infinite, token);
            }

            return arguments[0] == "rev-parse" ? "fixture" : string.Empty;
        });
        using var output = new StringWriter();

        // Act
        await ProjectSelectionPlan.Write(
            repository.Root, Path.Combine(repository.Root, "plan"), BaseRevision, null, processes.Run, output, TextWriter.Null, ct, time);

        // Assert
        time.Timers.ShouldAllSatisfy(static timer => timer.ShouldBe(TimeSpan.FromSeconds(30)));
        output.ToString().ShouldBe("Full validation: git diff " + BaseRevision + "...HEAD failed or did not finish within 30 seconds." + Environment.NewLine);
    }

    [Fact]
    public static async Task Selection_without_git_history_selects_every_test_project_and_reports_the_missing_revision_on_standard_error()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(static (_, _, _) => throw new InvalidOperationException("Not a Git checkout."));

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", BaseRevision], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var plan = await SelectionTestFiles.ReadPlan(Path.Combine(repository.Root, "TestResults", "selection"), ct);
        plan.Revision.ShouldBe("unavailable");
        plan.FullValidation.ShouldBeTrue();
        result.Error.ShouldContain("Selection revision unavailable", StringComparison.Ordinal);
        result.Output.ShouldNotContain("Selection revision unavailable", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("merge-base")]
    [InlineData("diff")]
    [InlineData("rev-parse")]
    public static async Task Selection_treats_a_git_failure_after_cancellation_as_the_cancellation(string gitCommand)
    {
        // Arrange
        using var repository = new RepositoryTestScope();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var processes = new ProcessLog(async (_, arguments, _) =>
        {
            if (arguments[0] == gitCommand)
            {
                // Ctrl+C also stops Git, which can fail before the token is observed.
                await cancellation.CancelAsync();
                throw new InvalidOperationException("Command failed (130): git " + gitCommand);
            }

            return arguments[0] == "rev-parse" ? "fixture" : "src/Core/Changed.cs\0";
        });

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", BaseRevision], processes.Run, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
        processes.Calls[^1].Arguments[0].ShouldBe(gitCommand);
        File.Exists(Path.Combine(repository.Root, "TestResults", "selection", "selection.json")).ShouldBeFalse();
    }

    [Fact]
    public static async Task Selection_writes_the_plan_to_the_selection_directory_under_the_root_and_removes_stale_filters()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.WriteFile("plans/current/removed.slnf", "{}");

        // Act
        var result = await RepositoryToolInvocation.Run(
            Path.GetDirectoryName(repository.Root)!, ["select", "--root", repository.Root, "--selection", "plans/current"], new ProcessLog().Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        string?[] files = [.. Directory.EnumerateFiles(Path.Combine(repository.Root, "plans", "current")).Select(Path.GetFileName).Order(StringComparer.Ordinal)];
        files.ShouldBe(["build.slnf", "library.slnf", "selection.json"]);
        Directory.Exists(Path.Combine(repository.Root, "TestResults")).ShouldBeFalse();
    }

    [Fact]
    public static async Task Changed_paths_are_escaped_in_the_reasons_on_standard_output()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(SelectionTestFiles.Git(diff: "src/Core/a\nb_required=true.cs\0"));

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", BaseRevision], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var lines = result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.ShouldNotContain(static line => line.StartsWith("b_required", StringComparison.Ordinal));
        lines[0].ShouldStartWith("src/Core/a", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, ".config/sharedkernel-ci.json does not exist.")]
    [InlineData("{\"version\": 1}", ".config/sharedkernel-ci.json is not valid at $ (line 1).")]
    public static async Task An_invalid_configuration_fails_with_the_domain_exit_code(string? configuration, string expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var path = Path.Combine(repository.Root, ".config", "sharedkernel-ci.json");
        if (configuration is null)
        {
            File.Delete(path);
        }
        else
        {
            await File.WriteAllTextAsync(path, configuration, ct);
        }

        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldStartWith(expected, StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public static async Task A_documentation_change_in_this_repository_is_documentation_only()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        using var scratch = new RepositoryTestScope();
        var selection = Path.Combine(scratch.Root, "plan");
        var githubOutput = Path.Combine(scratch.Root, "github-output.txt");

        // Act
        var result = await RepositoryToolInvocation.Run(
            root, ["select", "--base", BaseRevision, "--selection", selection, "--github-output", githubOutput], SelectionTestFiles.CheckoutGit("docs/development.md"), ct);

        // Assert
        var outputs = await File.ReadAllTextAsync(githubOutput, ct);
        result.ExitCode.ShouldBe(0, result.Error);
        outputs.ShouldStartWith("build_required=false\ndocumentation_only=true\nmatrix=[]\n", StringComparison.Ordinal);
    }
}
