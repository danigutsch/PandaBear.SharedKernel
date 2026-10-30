using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Checks the repo-config tool's command-line surface: help text, parse errors, the root option and documented command lines.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RepoConfigCommandLineTests
{
    [Fact]
    public async Task Root_help_lists_every_command()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["--help"], output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);
        var outputText = output.ToString();

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        outputText.ShouldContain("  init ", StringComparison.Ordinal);
        outputText.ShouldContain("  verify ", StringComparison.Ordinal);
        outputText.ShouldContain("  diff ", StringComparison.Ordinal);
        outputText.ShouldContain("  set <key> <value> ", StringComparison.Ordinal);
        outputText.ShouldContain("  get <query> ", StringComparison.Ordinal);
        outputText.ShouldContain("  github ", StringComparison.Ordinal);
        outputText.ShouldContain("  automation ", StringComparison.Ordinal);
        outputText.ShouldContain("  verify-text-encoding ", StringComparison.Ordinal);
        outputText.ShouldContain("  verify-podman-dns ", StringComparison.Ordinal);
        outputText.ShouldContain("  verify-json ", StringComparison.Ordinal);
        outputText.ShouldContain("  verify-line-endings ", StringComparison.Ordinal);
        outputText.ShouldContain("  verify-links ", StringComparison.Ordinal);
        outputText.ShouldContain("  verify-gherkin ", StringComparison.Ordinal);
        outputText.ShouldContain("  verify-action-pins ", StringComparison.Ordinal);
        outputText.ShouldContain("  security ", StringComparison.Ordinal);
        outputText.ShouldContain("  dependency ", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_command_is_a_parse_error()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run([], output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("Required command was not provided.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Version_option_writes_the_version_to_standard_output()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["--version"], output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);
        var outputText = output.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.Trim().ShouldNotBe(string.Empty);
        error.ToString().ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("github")]
    [InlineData("automation")]
    [InlineData("security")]
    [InlineData("dependency")]
    public async Task Command_groups_require_a_subcommand(string group)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run([group], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("Required command was not provided.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Root_option_defaults_to_the_working_directory()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["init"], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        workspace.FileExists("roadmap/config.json").ShouldBeTrue();
    }

    [Fact]
    public async Task Root_option_resolves_relative_paths_against_the_working_directory()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var workingDirectory = Path.GetDirectoryName(workspace.RootPath).ShouldNotBeNull();
        var relativeRoot = Path.GetFileName(workspace.RootPath);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["init", "--root", relativeRoot], output, error, workingDirectory, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        workspace.FileExists("roadmap/config.json").ShouldBeTrue();
    }

    [Fact]
    public async Task Dot_root_verifies_the_working_directory()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        using var diffOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var diffError = new StringWriter(CultureInfo.InvariantCulture);
        var initExitCode = await RepoConfigToolRunner.Run(["init", "--root", "."], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        initExitCode.ShouldBe(0);

        // Act
        var verifyExitCode = await RepoConfigToolRunner.Run(["verify", "--root", "."], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var diffExitCode = await RepoConfigToolRunner.Run(["diff", "--root", "."], diffOutput, diffError, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        verifyExitCode.ShouldBe(0);
        verifyOutput.ToString().ShouldContain("Repository config is valid.", StringComparison.Ordinal);
        verifyError.ToString().ShouldBe(string.Empty);
        diffExitCode.ShouldBe(0);
        diffOutput.ToString().ShouldContain("Repository config has no drift.", StringComparison.Ordinal);
        diffError.ToString().ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("github sync", "sync")]
    [InlineData("github sync --force --root .", "sync")]
    [InlineData("github reconcile --root .", "reconcile")]
    [InlineData("github reconcile --force --root .", "reconcile")]
    [InlineData("github import --root .", "import")]
    [InlineData("github import --force --root .", "import")]
    [InlineData("verify-text-encoding --root .", "verify-text-encoding")]
    [InlineData("verify-podman-dns", "verify-podman-dns")]
    [InlineData("verify-podman-dns --verbosity q", "verify-podman-dns")]
    [InlineData("get next-unblocked --type issue", "get")]
    [InlineData("get blockers-of RM-004", "get")]
    [InlineData("get by-label \"area: tooling\" --limit 3", "get")]
    [InlineData("set github.repository owner/repository --root .", "set")]
    [InlineData("automation plan-github --root .", "plan-github")]
    [InlineData("automation publish-result --root .", "publish-result")]
    [InlineData("automation publish-result --force --root .", "publish-result")]
    [InlineData("verify-json", "verify-json")]
    [InlineData("verify-json --root .", "verify-json")]
    [InlineData("verify-line-endings --root .", "verify-line-endings")]
    [InlineData("verify-links", "verify-links")]
    [InlineData("verify-links roadmap/README.md docs --root .", "verify-links")]
    [InlineData("verify-action-pins", "verify-action-pins")]
    [InlineData("verify-action-pins --manifest .github/action-pins.json --root .", "verify-action-pins")]
    [InlineData("verify-gherkin", "verify-gherkin")]
    [InlineData("verify-gherkin tests/Features tests/a.feature --root .", "verify-gherkin")]
    [InlineData("dependency submit --snapshot TestResults/dependency-snapshot.json --repository owner/repository", "submit")]
    [InlineData("dependency submit --snapshot snapshot.json --repository owner/repository --force --root .", "submit")]
    [InlineData("dependency verify-license-exceptions", "verify-license-exceptions")]
    [InlineData("dependency verify-license-exceptions --github-output \"$GITHUB_OUTPUT\" --root .", "verify-license-exceptions")]
    public void Command_lines_parse_to_their_command(string commandLine, string expectedCommand)
    {
        // Arrange
        var rootCommand = RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(Path.GetTempPath()));
        var args = DocumentedCommandLines.Arguments("sharedkernel-repo " + commandLine);

        // Act
        var parseResult = rootCommand.Parse(args);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        parseResult.CommandResult.Command.Name.ShouldBe(expectedCommand);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Blank_root_option_is_a_parse_error(string root)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["init", "--root", root], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("--root requires a directory path.", StringComparison.Ordinal);
        workspace.FileExists("roadmap/config.json").ShouldBeFalse();
    }

    [Fact]
    public async Task Root_option_with_a_null_character_from_a_response_file_is_a_parse_error()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var responseFilePath = Path.Combine(workspace.RootPath, "arguments.rsp");
        await File.WriteAllLinesAsync(responseFilePath, ["init", "--root", "a\0b"], TestContext.Current.CancellationToken);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["@" + responseFilePath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("--root path contains a null character.", StringComparison.Ordinal);
        workspace.FileExists("roadmap/config.json").ShouldBeFalse();
    }

    [Fact]
    public async Task Root_option_without_a_value_is_a_parse_error()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["init", "--root"], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("Required argument missing for option: '--root'.", StringComparison.Ordinal);
        workspace.FileExists("roadmap/config.json").ShouldBeFalse();
    }

    [Theory]
    [InlineData("sync")]
    [InlineData("reconcile")]
    [InlineData("import")]
    public async Task GitHub_command_help_describes_force_and_root(string command)
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", command, "--help"], output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);
        var outputText = output.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("--force", StringComparison.Ordinal);
        outputText.ShouldContain("--root <path>", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Publish_result_help_describes_force_and_root()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["automation", "publish-result", "--help"], output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);
        var outputText = output.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("Previews", StringComparison.Ordinal);
        outputText.ShouldContain("--force", StringComparison.Ordinal);
        outputText.ShouldContain("--root <path>", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Get_help_lists_every_query()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "--help"], output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);
        var outputText = output.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("next-priority", StringComparison.Ordinal);
        outputText.ShouldContain("next-unblocked", StringComparison.Ordinal);
        outputText.ShouldContain("next-work", StringComparison.Ordinal);
        outputText.ShouldContain("next-blockers", StringComparison.Ordinal);
        outputText.ShouldContain("next-enablers", StringComparison.Ordinal);
        outputText.ShouldContain("low-hanging-fruit", StringComparison.Ordinal);
        outputText.ShouldContain("pareto", StringComparison.Ordinal);
        outputText.ShouldContain("blocking-overview", StringComparison.Ordinal);
        outputText.ShouldContain("tags", StringComparison.Ordinal);
        outputText.ShouldContain("labels", StringComparison.Ordinal);
        outputText.ShouldContain("blockers-of", StringComparison.Ordinal);
        outputText.ShouldContain("by-tag", StringComparison.Ordinal);
        outputText.ShouldContain("by-label", StringComparison.Ordinal);
        outputText.ShouldContain("--limit <limit>", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Get_rejects_an_unknown_query_before_loading_the_roadmap()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "bogus"], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Argument 'bogus' not recognized.", StringComparison.Ordinal);
        errorText.ShouldNotContain("sharedkernel-repo:", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("blockers-of", null, "get blockers-of requires an item ID.")]
    [InlineData("by-tag", null, "get by-tag requires a tag.")]
    [InlineData("by-label", null, "get by-label requires a label.")]
    [InlineData("next-work", "RM-001", "get next-work does not take a value; found 'RM-001'.")]
    [InlineData("tags", "enabler", "get tags does not take a value; found 'enabler'.")]
    public async Task Get_validates_the_query_value_before_loading_the_roadmap(string query, string? value, string expectedError)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        string[] args = value is null ? ["get", query] : ["get", query, value];

        // Act
        var exitCode = await RepoConfigToolRunner.Run(args, output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain(expectedError, StringComparison.Ordinal);
        errorText.ShouldNotContain("sharedkernel-repo:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Set_help_lists_the_supported_key()
    {
        // Arrange
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["set", "--help"], output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("github.repository", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Documented_command_lines_parse_without_errors()
    {
        // Arrange
        var commandLines = DocumentedCommandLines.Find(DocumentedCommandLines.ReadToolReadme())
            .Concat(DocumentedCommandLines.Find(RoadmapTemplates.RoadmapReadme))
            .ToArray();
        var rootCommand = RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(Path.GetTempPath()));

        // Act
        var parseErrors = commandLines
            .SelectMany(commandLine => rootCommand
                .Parse(DocumentedCommandLines.Arguments(commandLine))
                .Errors
                .Select(parseError => $"{commandLine}: {parseError.Message}"))
            .ToArray();

        // Assert
        commandLines.ShouldNotBeEmpty();
        parseErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Diagram_directive_parses_a_valid_command_line_without_running_it()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["[diagram]", "init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("[ init [ --root <", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
        workspace.FileExists("roadmap/config.json").ShouldBeFalse("The directive prints the parse diagram instead of running init.");
    }

    [Fact]
    public async Task Diagram_directive_fails_a_misspelled_option_without_running_the_command()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["[diagram]", "init", "--rot", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldContain("--rot", StringComparison.Ordinal);
        workspace.FileExists("roadmap/config.json").ShouldBeFalse("A diagram never runs the command, even when parsing fails.");
    }
}
