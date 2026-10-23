using PandaBear.Testing;

namespace PandaBear.Versioning.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class VersioningCommandLineTests
{
    private static readonly string[] CommandNames =
    [
        "classify-commit",
        "compute",
        "calculate-release",
        "check-public-api-baselines",
        "validate-package-metadata",
        "has-breaking-change-marker",
        "check-api-compatibility",
        "pack-sharedkernel",
        "prepare-release",
        "create-candidate",
        "validate-candidate",
        "consume-candidate",
        "consume-packages",
        "stage-candidate",
    ];

    [Fact]
    public static async Task Root_help_lists_every_command_on_standard_output()
    {
        // Act
        var result = await VersioningCommandLineRunner.Invoke(["--help"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBe(string.Empty);
        result.Output.ShouldContain("Usage:", StringComparison.Ordinal);
        foreach (var command in CommandNames)
        {
            result.Output.ShouldContain("  " + command + " ", StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("classify-commit", "<message>...")]
    [InlineData("compute", "--base <version> (REQUIRED)", "--prerelease <label>", "--sha <sha>")]
    [InlineData("calculate-release", "--version-kind <prerelease|stable>", "--run-number <number>", "--sha <sha>", "--github-output <path>", "--github-summary <path>", "--repo-root <path>")]
    [InlineData("check-public-api-baselines", "--repo-root <path>")]
    [InlineData("validate-package-metadata", "--repo-root <path>")]
    [InlineData("has-breaking-change-marker", "<git-range>", "--repo-root <path>")]
    [InlineData("check-api-compatibility", "--version <version>", "--release-phase <alpha|beta|rc|stable>", "--baseline-version <version>", "--breaking-marker", "--output-root <path>", "--repo-root <path>")]
    [InlineData("pack-sharedkernel", "--version <version>", "--assembly-version <version>", "--file-version <version>", "--informational-version <version>", "--output-root <path>", "--skip-restore-check", "--repo-root <path>")]
    [InlineData("prepare-release", "--version <version> (REQUIRED)", "--package-dir <path> (REQUIRED)", "--output-dir <path>", "--source-tag <tag>", "--release-impact <impact>", "--sha <sha>", "--repo-root <path>")]
    [InlineData("create-candidate", "--version <version>", "--baseline-version <version>", "--repo-root <path>")]
    [InlineData("validate-candidate", "--version <version>", "--baseline-version <version>", "--aot", "--repo-root <path>")]
    [InlineData("consume-candidate", "--candidate <path>", "--candidate-directory <path>", "--output <path>", "--aot", "--repo-root <path>")]
    [InlineData("consume-packages", "--feed <path> (REQUIRED)", "--version <version> (REQUIRED)", "--aot", "--repo-root <path>")]
    [InlineData("stage-candidate", "--candidate <path> (REQUIRED)", "--feed <path> (REQUIRED)", "--output <path> (REQUIRED)", "--repo-root <path>")]
    public static async Task Command_help_lists_arguments_and_options_on_standard_output(string command, params string[] expected)
    {
        // Act
        var result = await VersioningCommandLineRunner.Invoke([command, "--help"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBe(string.Empty);
        result.Output.ShouldContain(" " + command, StringComparison.Ordinal);
        result.Output.ShouldContainInOrder(expected);
    }

    [Fact]
    public static async Task Version_option_prints_the_version_on_standard_output()
    {
        // Act
        var result = await VersioningCommandLineRunner.Invoke(["--version"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBe(string.Empty);
        result.Output.Trim().ShouldNotBe(string.Empty);
    }

    [Fact]
    public static async Task Missing_command_is_a_parse_error()
    {
        // Act
        var result = await VersioningCommandLineRunner.Invoke([], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("Required command was not provided.", StringComparison.Ordinal);
        result.Output.ShouldContain("Usage:", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Unrecognized command or argument 'unknown'.", "unknown")]
    [InlineData("Unrecognized command or argument '--unknown'.", "check-public-api-baselines", "--unknown", "value")]
    [InlineData("Unrecognized command or argument '--aot'.", "stage-candidate", "--candidate", "candidate.json", "--feed", "packages", "--output", "feed", "--aot")]
    [InlineData("Required argument missing for option: '--version'.", "check-api-compatibility", "--version")]
    [InlineData("Required argument missing for command: 'classify-commit'.", "classify-commit")]
    [InlineData("Required argument missing for command: 'has-breaking-change-marker'.", "has-breaking-change-marker")]
    [InlineData("Option '--base' is required.", "compute")]
    [InlineData("Option '--version' is required.", "prepare-release", "--package-dir", "packages")]
    [InlineData("Option '--package-dir' is required.", "prepare-release", "--version", "1.2.3")]
    [InlineData("Option '--feed' is required.", "consume-packages", "--version", "1.2.3")]
    [InlineData("Option '--candidate' is required.", "stage-candidate", "--feed", "packages", "--output", "feed")]
    [InlineData("Option '--output' is required.", "stage-candidate", "--candidate", "candidate.json", "--feed", "packages")]
    [InlineData("Argument 'beta' not recognized.", "calculate-release", "--version-kind", "beta")]
    [InlineData("Argument 'production' not recognized.", "calculate-release", "--version-kind", "production")]
    [InlineData("Argument 'gamma' not recognized.", "check-api-compatibility", "--release-phase", "gamma")]
    [InlineData("Option '--base' expects a semantic version, but got 'not-a-version'.", "compute", "--base", "not-a-version")]
    [InlineData("Option '--version' expects a semantic version, but got '1.2'.", "create-candidate", "--version", "1.2")]
    [InlineData("Option '--baseline-version' expects a semantic version, but got 'latest'.", "validate-candidate", "--baseline-version", "latest")]
    [InlineData("Option '--version' expects a semantic version, but got 'v1'.", "consume-packages", "--feed", "packages", "--version", "v1")]
    [InlineData("Option '--candidate' or '--candidate-directory' is required.", "consume-candidate")]
    [InlineData("Options '--candidate' and '--candidate-directory' cannot be combined.", "consume-candidate", "--candidate", "candidate.json", "--candidate-directory", "candidates")]
    public static async Task Invalid_command_lines_are_parse_errors_on_standard_error(string expectedError, params string[] args)
    {
        // Act
        var result = await VersioningCommandLineRunner.Invoke(args, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("classify-commit", "classify-commit", "feat(api)!:", "remove", "route")]
    [InlineData("compute", "compute", "--base", "0.1.0", "--prerelease", "alpha.1", "--sha", "abc123")]
    [InlineData("calculate-release", "calculate-release", "--repo-root", "/repo", "--version-kind", "stable", "--run-number", "42.1", "--sha", "abc123", "--github-output", "output.txt", "--github-summary", "summary.md")]
    [InlineData("check-public-api-baselines", "check-public-api-baselines", "--repo-root", "/repo")]
    [InlineData("validate-package-metadata", "validate-package-metadata", "--repo-root", "/repo")]
    [InlineData("has-breaking-change-marker", "has-breaking-change-marker", "origin/main..HEAD", "--repo-root", "/repo")]
    [InlineData("check-api-compatibility", "check-api-compatibility", "--version", "1.2.3", "--release-phase", "stable", "--baseline-version", "1.2.2", "--breaking-marker", "--output-root", "artifacts/api", "--repo-root", "/repo")]
    [InlineData("pack-sharedkernel", "pack-sharedkernel", "--version", "1.2.3", "--assembly-version", "1.0.0.0", "--file-version", "1.2.3.0", "--informational-version", "1.2.3+sha.abc123", "--output-root", "artifacts", "--repo-root", "/repo", "--skip-restore-check")]
    [InlineData("prepare-release", "prepare-release", "--version", "1.2.3", "--package-dir", "packages", "--output-dir", "release", "--repo-root", "/repo", "--source-tag", "v1.2.2", "--release-impact", "minor", "--sha", "abc123")]
    [InlineData("create-candidate", "create-candidate", "--version", "1.2.3-alpha.1", "--baseline-version", "1.2.2", "--repo-root", "/repo")]
    [InlineData("validate-candidate", "validate-candidate", "--repo-root", "/repo", "--version", "1.2.3-alpha.1", "--baseline-version", "1.2.2", "--aot")]
    [InlineData("consume-candidate", "consume-candidate", "--candidate", "candidate.json", "--output", "evidence.json", "--aot", "--repo-root", "/repo")]
    [InlineData("consume-candidate", "consume-candidate", "--candidate-directory", "artifacts/candidate", "--aot")]
    [InlineData("consume-packages", "consume-packages", "--feed", "packages", "--version", "1.2.3", "--aot", "--repo-root", "/repo")]
    [InlineData("stage-candidate", "stage-candidate", "--repo-root", "/library", "--candidate", "candidate.json", "--feed", "packages", "--output", ".local/sharedkernel-feed")]
    public static void Command_lines_dispatch_to_their_command(string command, params string[] args)
    {
        // Act
        var parseResult = VersioningCommandLineRunner.Parse(args);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        parseResult.CommandResult.Command.Name.ShouldBe(command);
        var action = parseResult.CommandResult.Command.Action.ShouldNotBeNull();
        parseResult.Action.ShouldBeSameAs(action);
    }

    [Theory]
    [InlineData("--version-kind", "prerelease", "calculate-release")]
    [InlineData("--version-kind", "stable", "calculate-release", "--version-kind", "stable")]
    [InlineData("--run-number", "7.1", "calculate-release", "--run-number", "7.1")]
    [InlineData("--sha", "abc123", "calculate-release", "--sha", "abc123")]
    [InlineData("--sha", "abc123", "compute", "--base", "0.1.0", "--sha", "abc123")]
    [InlineData("--repo-root", ".", "calculate-release")]
    [InlineData("--repo-root", ".", "stage-candidate", "--candidate", "candidate.json", "--feed", "packages", "--output", "feed")]
    [InlineData("--release-phase", "alpha", "check-api-compatibility")]
    [InlineData("--output-root", "artifacts/api-compat", "check-api-compatibility")]
    [InlineData("--output-root", "artifacts/packages/local", "pack-sharedkernel")]
    [InlineData("--output-dir", "artifacts/release-prep", "prepare-release", "--version", "1.2.3", "--package-dir", "packages")]
    public static void String_options_bind_values_and_defaults(string option, string expected, params string[] args)
    {
        // Act
        var parseResult = VersioningCommandLineRunner.Parse(args);
        var value = parseResult.GetValue<string>(option);

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, "consume-candidate", "--candidate-directory", "artifacts/candidate", "--aot")]
    [InlineData(true, "consume-candidate", "--candidate", "candidate.json", "--aot")]
    [InlineData(false, "consume-candidate", "--candidate-directory", "artifacts/candidate")]
    [InlineData(true, "validate-candidate", "--aot")]
    [InlineData(false, "validate-candidate")]
    [InlineData(true, "consume-packages", "--feed", "packages", "--version", "1.2.3", "--aot")]
    [InlineData(false, "consume-packages", "--feed", "packages", "--version", "1.2.3")]
    public static void Aot_option_binds_true_only_when_present(bool expected, params string[] args)
    {
        // Act
        var parseResult = VersioningCommandLineRunner.Parse(args);
        var aot = parseResult.GetValue<bool>("--aot");

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        aot.ShouldBe(expected);
    }

    [Fact]
    public static void Semantic_version_options_bind_parsed_versions()
    {
        // Act
        var parseResult = VersioningCommandLineRunner.Parse("create-candidate", "--version", "1.2.3-alpha.1+sha.abc", "--baseline-version", "1.2.2");
        var version = parseResult.GetValue<SemanticVersion>("--version");
        var baselineVersion = parseResult.GetValue<SemanticVersion>("--baseline-version");

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        version.ShouldBe(new SemanticVersion(1, 2, 3, "alpha.1", "sha.abc"));
        baselineVersion.ShouldBe(new SemanticVersion(1, 2, 2));
    }

    [Fact]
    public static void Classify_commit_accepts_a_message_split_across_arguments()
    {
        // Act
        var parseResult = VersioningCommandLineRunner.Parse("classify-commit", "feat(api)!:", "remove", "route");
        var message = parseResult.GetValue<string[]>("message");

        // Assert
        parseResult.Errors.ShouldBeEmpty();
        message.ShouldBe(["feat(api)!:", "remove", "route"]);
    }

    [Fact]
    public static async Task Command_failures_exit_with_code_2_and_report_on_standard_error()
    {
        // Act
        var result = await VersioningCommandLineRunner.Invoke(["classify-commit", "invalid"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldStartWith("Error: ", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Consume_candidate_writes_child_process_output_to_standard_error()
    {
        // Arrange
        var sampleOutput = "Consumed sample " + Guid.NewGuid().ToString("N");
        var resultPrefix = "Package consumption checks passed. Evidence workspace: ";
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        await temporary.WriteConsumptionSamples(sampleOutput, TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);
        using var evidence = new EvidenceWorkspaces(result);

        // Assert
        var succeeded = result.ExitCode == 0;
        succeeded.ShouldBeTrue($"consume-candidate exited with {result.ExitCode}. Standard error:{Environment.NewLine}{result.Error}");
        result.Error.ShouldContain(sampleOutput, StringComparison.Ordinal);
        result.Output.ShouldNotContain(sampleOutput, StringComparison.Ordinal);
        result.Output.ShouldStartWith(resultPrefix, StringComparison.Ordinal);
        Directory.Exists(result.Output[resultPrefix.Length..].Trim()).ShouldBeTrue();
    }

    [Fact]
    public static async Task Canceled_commands_exit_with_code_1_without_staging_packages()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        string[] args =
        [
            "stage-candidate",
            "--repo-root",
            temporary.Root,
            "--candidate",
            manifest,
            "--feed",
            temporary.PackageDirectory,
            "--output",
            temporary.OutputDirectory,
        ];

        // Act
        var result = await VersioningCommandLineRunner.Invoke(args, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
        Directory.Exists(temporary.OutputDirectory).ShouldBeFalse();
    }

    [Fact]
    public static void Workflow_versioning_command_lines_dispatch_to_a_command()
    {
        // Arrange
        var workflow = Path.Combine(RepositoryRoot.Find(), ".github", "workflows", "ci.yml");
        var commandLines = CallerCommandLine.ReadWorkflowCommandLines(workflow);

        // Act
        var parseResults = commandLines.Select(static line => VersioningCommandLineRunner.Parse(CallerCommandLine.Split(line))).ToArray();

        // Assert
        commandLines.ShouldContain(static line => line.StartsWith("calculate-release ", StringComparison.Ordinal));
        commandLines.ShouldContain(static line => line.StartsWith("create-candidate ", StringComparison.Ordinal));
        commandLines.ShouldContain(static line => line.StartsWith("consume-candidate ", StringComparison.Ordinal));
        foreach (var parseResult in parseResults)
        {
            parseResult.Errors.ShouldBeEmpty();
            var action = parseResult.CommandResult.Command.Action.ShouldNotBeNull();
            parseResult.Action.ShouldBeSameAs(action);
        }
    }

    [Fact]
    public static void Readme_command_lines_dispatch_to_their_command()
    {
        // Arrange
        var readme = Path.Combine(RepositoryRoot.Find(), "tools", "SharedKernel.Versioning.Tool", "README.md");
        var commandLines = CallerCommandLine.ReadReadmeCommandLines(readme);

        // Act
        var parsed = commandLines
            .Select(static commandLine => (CommandLine: commandLine, Arguments: CallerCommandLine.Split(commandLine)))
            .Select(static entry => (entry.CommandLine, Expected: entry.Arguments[0], Result: VersioningCommandLineRunner.Parse(entry.Arguments)))
            .ToArray();

        // Assert
        // The piped compute and redirected prepare-release examples read standard input.
        commandLines.ShouldContain(static line => line.StartsWith("compute ", StringComparison.Ordinal));
        commandLines.ShouldContain(static line => line.StartsWith("prepare-release ", StringComparison.Ordinal));
        parsed.ShouldAllSatisfy(static entry =>
        {
            entry.Result.Errors.ShouldBeEmpty();
            CallerCommandLine.Selection(entry.Result).ShouldBe(entry.Expected);
        });
    }

    [Theory]
    [InlineData("has-breaking-change-marker", "--output=x")]
    [InlineData("has-breaking-change-marker", "--", "--output=x")]
    [InlineData("has-breaking-change-marker", "-p")]
    [InlineData("has-breaking-change-marker", "")]
    [InlineData("has-breaking-change-marker", " ")]
    public static void Breaking_change_marker_rejects_a_range_that_git_would_not_read_as_a_revision(params string[] args)
    {
        // Act
        var parseResult = VersioningCommandLineRunner.Parse(args);

        // Assert
        parseResult.Errors.ShouldHaveSingleItem().Message.ShouldBe(
            "<git-range> requires a revision range that is not empty and does not start with '-'.");
    }

    [Fact]
    public static async Task Failure_after_cancellation_exits_with_code_1_without_output()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var input = new CancelingTextReader(cancellation, new InvalidOperationException("Command failed (130): git log"));

        // Act
        var result = await VersioningCommandLineRunner.Invoke(["compute", "--base", "0.1.0"], input, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
    }
}
