namespace SharedKernel.Testing.CodeFixRunner.Tests;

/// <summary>Tests the code fix runner command line: option parsing, target validation, exit codes and what is written to standard output and error.</summary>
public sealed class CodeFixRunnerCommandLineTests : IDisposable
{
    private const string ProjectToken = "{project}";

    private readonly TemporaryDirectory projectDirectory = new();

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("--diagnostic", "SKTEST006", "--help", "sample.csproj")]
    [InlineData("sample.csproj", "-h")]
    public async Task Help_option_writes_usage_to_standard_output(params string[] args)
    {
        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, args, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Usage:", StringComparison.Ordinal);
        result.Output.ShouldContain("<project-or-solution>", StringComparison.Ordinal);
        result.Output.ShouldContain("--diagnostic <id>", StringComparison.Ordinal);
        result.Output.ShouldContain("[default: SKTEST004]", StringComparison.Ordinal);
        result.Error.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("sample.csproj", "--version")]
    public async Task Version_option_writes_version_to_standard_output(params string[] args)
    {
        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, args, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        string.IsNullOrWhiteSpace(result.Output).ShouldBeFalse(result.Output);
        result.Error.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData]
    [InlineData("--diagnostic")]
    [InlineData("--unknown", "SKTEST006", "sample.csproj")]
    [InlineData("sample.csproj", "extra")]
    public async Task Invalid_arguments_write_errors_to_standard_error_and_return_one(params string[] args)
    {
        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, args, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        string.IsNullOrWhiteSpace(result.Error).ShouldBeFalse(result.Error);
        result.Output.ShouldNotContain("Fixed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_target_file_is_rejected()
    {
        // Arrange
        var targetPath = Path.Combine(projectDirectory.Path, "Missing.Tests.csproj");

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, [targetPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(targetPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Target_without_project_or_solution_extension_is_rejected()
    {
        // Arrange
        var targetPath = Path.Combine(projectDirectory.Path, "sample.txt");
        await File.WriteAllTextAsync(targetPath, string.Empty, TestContext.Current.CancellationToken);

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, [targetPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain($"'{targetPath}' is not a .csproj, .sln, or .slnx file.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Sample.Tests.csproj")]
    [InlineData("Sample.sln")]
    [InlineData("Sample.slnx")]
    public async Task Project_and_solution_targets_are_accepted(string fileName)
    {
        // Arrange
        var targetPath = Path.Combine(projectDirectory.Path, fileName);
        await File.WriteAllTextAsync(targetPath, string.Empty, TestContext.Current.CancellationToken);

        // Act
        var parseResult = CodeFixRunnerCommandLine.Create(projectDirectory.Path, CodeFixRunEngine.Run).Parse([targetPath]);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Relative_target_resolves_against_the_working_directory()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "src"));
        var targetPath = Path.GetFullPath(Path.Combine(projectDirectory.Path, "src", "Sample.Tests.csproj"));
        await File.WriteAllTextAsync(targetPath, string.Empty, TestContext.Current.CancellationToken);
        var targetPaths = new List<string>();

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(
            projectDirectory.Path,
            [Path.Combine("src", "Sample.Tests.csproj")],
            (options, _, _) =>
            {
                targetPaths.Add(options.TargetPath);
                return Task.FromResult<int?>(0);
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBe(string.Empty);
        targetPaths.ShouldHaveSingleItem().ShouldBe(targetPath);
    }

    [Fact]
    public async Task Directory_with_a_project_extension_is_rejected()
    {
        // Arrange
        var targetPath = Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "Sample.Tests.csproj")).FullName;
        var targetPaths = new List<string>();

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(
            projectDirectory.Path,
            [targetPath],
            (options, _, _) =>
            {
                targetPaths.Add(options.TargetPath);
                return Task.FromResult<int?>(0);
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain($"File does not exist: '{targetPath}'.", StringComparison.Ordinal);
        targetPaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task Target_with_a_null_character_is_rejected()
    {
        // Arrange
        var responseFilePath = Path.Combine(projectDirectory.Path, "target.rsp");
        await File.WriteAllTextAsync(responseFilePath, "Sample\0.Tests.csproj", TestContext.Current.CancellationToken);
        var targetPaths = new List<string>();

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(
            projectDirectory.Path,
            ["@" + responseFilePath],
            (options, _, _) =>
            {
                targetPaths.Add(options.TargetPath);
                return Task.FromResult<int?>(0);
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("File does not exist: 'Sample\0.Tests.csproj'.", StringComparison.Ordinal);
        targetPaths.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SKTEST007")]
    [InlineData("SKTEST008")]
    [InlineData("sktest006")]
    public async Task Diagnostic_without_code_fix_is_rejected_and_project_is_unchanged(string diagnosticId)
    {
        // Arrange
        var (projectPath, sourcePath) = await CodeFixRunnerTestProject.Write(
            projectDirectory.Path,
            CodeFixRunnerTestProject.UnorderedMarkersSourceFile,
            TestContext.Current.CancellationToken);

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, ["--diagnostic", diagnosticId, projectPath], TestContext.Current.CancellationToken);
        var source = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

        // Assert
        source.ShouldBe(CodeFixRunnerTestProject.UnorderedMarkersSourceFile);
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain($"'{diagnosticId}'", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnostic_option_accepts_SKTEST017()
    {
        // Arrange
        var targetPath = Path.Combine(projectDirectory.Path, "Sample.Tests.csproj");
        await File.WriteAllTextAsync(targetPath, string.Empty, TestContext.Current.CancellationToken);

        // Act
        var parseResult = CodeFixRunnerCommandLine.Create(projectDirectory.Path, CodeFixRunEngine.Run).Parse(["--diagnostic", "SKTEST017", targetPath]);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--diagnostic", "SKTEST006", ProjectToken)]
    [InlineData(ProjectToken, "--diagnostic", "SKTEST006")]
    [InlineData("--diagnostic=SKTEST006", ProjectToken)]
    public async Task Requested_diagnostic_is_fixed_and_count_is_written_to_standard_output(params string[] args)
    {
        // Arrange
        var (projectPath, sourcePath) = await CodeFixRunnerTestProject.Write(
            projectDirectory.Path,
            CodeFixRunnerTestProject.SourceFile,
            TestContext.Current.CancellationToken);
        var commandLine = args
            .Select(argument => string.Equals(argument, ProjectToken, StringComparison.Ordinal) ? projectPath : argument)
            .ToArray();

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, commandLine, TestContext.Current.CancellationToken);
        var source = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe($"Fixed 1 SKTEST006 diagnostic(s).\n");
        result.Error.ShouldBe(string.Empty);
        source.ShouldContain("(true).ShouldBeTrue()", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diagnostic_defaults_to_SKTEST004()
    {
        // Arrange
        var (projectPath, sourcePath) = await CodeFixRunnerTestProject.Write(
            projectDirectory.Path,
            CodeFixRunnerTestProject.HelperSourceFile,
            TestContext.Current.CancellationToken);

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, [projectPath], TestContext.Current.CancellationToken);
        var source = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe($"Fixed 1 SKTEST004 diagnostic(s).\n");
        result.Error.ShouldBe(string.Empty);
        source.ShouldContain("SampleTestsHelpers.CreateTourId()", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Helper_file_fix_can_be_retried_in_a_default_compile_project()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        CodeFixRunnerTestProject.PinRepositorySdk(projectDirectory.Path);
        var (projectPath, _) = await CodeFixRunnerTestProject.Write(
            projectDirectory.Path,
            CodeFixRunnerTestProject.HelperSourceFile,
            ct);
        var originalProject = await File.ReadAllTextAsync(projectPath, ct);

        // Act
        var applied = await CodeFixRunnerProcess.Run(projectDirectory.Path, [projectPath], ct);
        var retried = await CodeFixRunnerProcess.Run(projectDirectory.Path, [projectPath], ct);

        // Assert
        applied.ExitCode.ShouldBe(0, applied.Error);
        applied.Output.ShouldBe("Fixed 1 SKTEST004 diagnostic(s).\n");
        retried.ExitCode.ShouldBe(0, retried.Error);
        retried.Output.ShouldBe("Fixed 0 SKTEST004 diagnostic(s).\n");
        retried.Error.ShouldBe(string.Empty);
        var updatedProject = await File.ReadAllTextAsync(projectPath, ct);
        updatedProject.ShouldBe(originalProject);
    }

    [Theory]
    [InlineData("<PropertyGroup><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include=\"SampleTests.cs\" /></ItemGroup>")]
    [InlineData("<PropertyGroup><DefaultItemExcludes>$(DefaultItemExcludes);SampleTestsHelpers.cs</DefaultItemExcludes></PropertyGroup>")]
    [InlineData("<ItemGroup Condition=\"'$(Configuration)' == 'Debug'\"><Compile Remove=\"SampleTestsHelpers.cs\" /></ItemGroup>")]
    public async Task Helper_file_fix_preserves_compile_membership_across_configurations(string items)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        CodeFixRunnerTestProject.PinRepositorySdk(projectDirectory.Path);
        var (projectPath, _) = await CodeFixRunnerTestProject.Write(projectDirectory.Path, CodeFixRunnerTestProject.HelperSourceFile, ct);
        await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.ProjectFile.Replace("</Project>", items + "</Project>", StringComparison.Ordinal), ct);

        // Act
        var applied = await CodeFixRunnerProcess.Run(projectDirectory.Path, [projectPath], ct);
        var retried = await CodeFixRunnerProcess.Run(projectDirectory.Path, [projectPath], ct);
        var debugCount = await CodeFixRunnerProcess.CountHelperCompileItems(projectPath, "Debug", ct);
        var releaseCount = await CodeFixRunnerProcess.CountHelperCompileItems(projectPath, "Release", ct);

        // Assert
        applied.ExitCode.ShouldBe(0, applied.Error);
        applied.Output.ShouldBe("Fixed 1 SKTEST004 diagnostic(s).\n");
        retried.ExitCode.ShouldBe(0, retried.Error);
        retried.Output.ShouldBe("Fixed 0 SKTEST004 diagnostic(s).\n");
        debugCount.ShouldBe(1);
        releaseCount.ShouldBe(1);
    }

    [Fact]
    public async Task Failed_compile_membership_leaves_the_source_and_project_usable()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        CodeFixRunnerTestProject.PinRepositorySdk(projectDirectory.Path);
        var (projectPath, sourcePath) = await CodeFixRunnerTestProject.Write(projectDirectory.Path, CodeFixRunnerTestProject.HelperSourceFile, ct);
        var targets = Path.Combine(projectDirectory.Path, "Directory.Build.targets");
        await File.WriteAllTextAsync(targets, """
            <Project><ItemGroup><Compile Remove="SampleTestsHelpers.cs" /></ItemGroup></Project>
            """, ct);

        // Act
        var failed = await CodeFixRunnerProcess.Run(projectDirectory.Path, [projectPath], ct);
        var projectAfterFailure = await File.ReadAllTextAsync(projectPath, ct);
        var sourceAfterFailure = await File.ReadAllTextAsync(sourcePath, ct);
        var helperAfterFailure = File.Exists(Path.Combine(projectDirectory.Path, "SampleTestsHelpers.cs"));
        File.Delete(targets);
        var retried = await CodeFixRunnerProcess.Run(projectDirectory.Path, [projectPath], ct);

        // Assert
        failed.ExitCode.ShouldBe(1, failed.Error);
        failed.Error.ShouldContain("The new source file is not compiled in every evaluated build context", StringComparison.Ordinal);
        projectAfterFailure.ShouldBe(CodeFixRunnerTestProject.ProjectFile);
        sourceAfterFailure.ShouldBe(CodeFixRunnerTestProject.HelperSourceFile);
        helperAfterFailure.ShouldBeFalse();
        retried.ExitCode.ShouldBe(0, retried.Error);
        retried.Output.ShouldBe("Fixed 1 SKTEST004 diagnostic(s).\n");
    }

    [Fact]
    public async Task Diagnostics_without_available_fix_are_reported_on_standard_error()
    {
        // Arrange
        var (projectPath, _) = await CodeFixRunnerTestProject.Write(
            projectDirectory.Path,
            CodeFixRunnerTestProject.SupportedAndUnsupportedSourceFile,
            TestContext.Current.CancellationToken);

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, ["--diagnostic", "SKTEST006", projectPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe($"Fixed 1 SKTEST006 diagnostic(s).\n");
        result.Error.ShouldContain("No code fix available for SKTEST006", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Project_that_does_not_load_returns_two_and_reports_the_failure_on_standard_error()
    {
        // Arrange
        var projectPath = Path.Combine(projectDirectory.Path, "Malformed.Tests.csproj");
        await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.MalformedProjectFile, TestContext.Current.CancellationToken);

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, [projectPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldContain("Malformed.Tests.csproj", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Solution_with_project_that_does_not_load_returns_two_and_leaves_sources_unchanged()
    {
        // Arrange
        var (_, sourcePath) = await CodeFixRunnerTestProject.Write(
            projectDirectory.Path,
            CodeFixRunnerTestProject.SourceFile,
            TestContext.Current.CancellationToken);
        var solutionPath = Path.Combine(projectDirectory.Path, "Sample.slnx");
        await File.WriteAllTextAsync(solutionPath, CodeFixRunnerTestProject.SolutionWithMissingProjectFile, TestContext.Current.CancellationToken);

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, ["--diagnostic", "SKTEST006", solutionPath], TestContext.Current.CancellationToken);
        var source = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

        // Assert
        source.ShouldBe(CodeFixRunnerTestProject.SourceFile);
        result.ExitCode.ShouldBe(2);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldContain("Missing.Tests.csproj", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Working_directory_global_JSON_that_pins_a_missing_sdk_returns_two_with_a_one_line_error()
    {
        // Arrange
        var (_, sourcePath) = await CodeFixRunnerTestProject.Write(
            projectDirectory.Path,
            CodeFixRunnerTestProject.SourceFile,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory.Path, "global.json"),
            CodeFixRunnerTestProject.MissingSdkGlobalJsonFile,
            TestContext.Current.CancellationToken);

        // Act
        var result = await CodeFixRunnerProcess.Run(
            projectDirectory.Path,
            ["--diagnostic", "SKTEST006", "Sample.Tests.csproj"],
            TestContext.Current.CancellationToken);
        var source = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldContain("No fixes were applied because MSBuild could not be loaded from a .NET SDK: ", StringComparison.Ordinal);
        result.Error.ShouldContain("A compatible .NET SDK was not found.", StringComparison.Ordinal);
        result.Error.ShouldNotContain("Microsoft.Build.Framework", StringComparison.Ordinal);
        result.Error.TrimEnd().ShouldNotContain("\n", StringComparison.Ordinal);
        result.ExitCode.ShouldBe(2);
        result.Output.ShouldNotContain("Fixed", StringComparison.Ordinal);
        source.ShouldBe(CodeFixRunnerTestProject.SourceFile);
    }

    [Fact]
    public async Task Cancelled_run_returns_one_and_leaves_project_unchanged()
    {
        // Arrange
        var (projectPath, sourcePath) = await CodeFixRunnerTestProject.Write(
            projectDirectory.Path,
            CodeFixRunnerTestProject.SourceFile,
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(projectDirectory.Path, ["--diagnostic", "SKTEST006", projectPath], cancellation.Token);
        var source = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);

        // Assert
        source.ShouldBe(CodeFixRunnerTestProject.SourceFile);
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Failure_after_cancellation_returns_one_without_output()
    {
        // Arrange
        var targetPath = Path.Combine(projectDirectory.Path, "Sample.Tests.csproj");
        await File.WriteAllTextAsync(targetPath, string.Empty, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(
            projectDirectory.Path,
            [targetPath],
            async (_, _, _) =>
            {
                // A signal also stops the MSBuild build host, so loading fails with a connection error instead of a cancellation.
                await cancellation.CancelAsync();
                throw new IOException("Connection reset by peer");
            },
            cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Load_failure_reported_after_cancellation_returns_one_without_output()
    {
        // Arrange
        var projectPath = Path.Combine(projectDirectory.Path, "Malformed.Tests.csproj");
        await File.WriteAllTextAsync(projectPath, CodeFixRunnerTestProject.MalformedProjectFile, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(
            projectDirectory.Path,
            [projectPath],
            (options, error, ct) => CodeFixRunEngine.Runner.Run(
                options.TargetPath,
                options.DiagnosticId,
                error,
                async (workspace, targetPath, _) =>
                {
                    // A signal as loading ends also stops the MSBuild build host, which the workspace reports as a load
                    // failure instead of throwing. The malformed project leaves such a failure before the token is canceled.
                    var project = await workspace.OpenProjectAsync(targetPath, cancellationToken: TestContext.Current.CancellationToken);
                    await cancellation.CancelAsync();
                    return project.Solution;
                },
                ct),
            cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Failure_without_cancellation_returns_one_and_reports_the_exception_on_standard_error()
    {
        // Arrange
        var targetPath = Path.Combine(projectDirectory.Path, "Sample.Tests.csproj");
        await File.WriteAllTextAsync(targetPath, string.Empty, TestContext.Current.CancellationToken);

        // Act
        var result = await CodeFixRunnerInvocation.Invoke(
            projectDirectory.Path,
            [targetPath],
            static (_, _, _) => Task.FromException<int?>(new IOException("Connection reset by peer")),
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldContain("System.IO.IOException: Connection reset by peer", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        projectDirectory.Dispose();
    }
}
