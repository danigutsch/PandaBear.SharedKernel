namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class VerifyGherkinCommandTests
{
    private const string Heading = "Repository Gherkin verification failed:";
    private const string ValidFeature = "Feature: Valid\n    Scenario: s\n        Given x\n";
    private const string InvalidFeature = "Feature:\n";

    [Fact]
    public async Task Verify_gherkin_reports_each_violation_in_the_seeded_corpus()
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(GherkinCorpus.Files, GherkinCorpus.Configuration);

        // Act
        var result = await repository.RunCommand(["verify-gherkin", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardOutput.ShouldBe(string.Empty);
        GherkinTestRepository.ErrorLines(result).ShouldBe([Heading, .. GherkinCorpus.Issues]);
    }

    [Fact]
    public async Task Verify_gherkin_checks_only_the_feature_files_under_explicit_paths()
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(GherkinCorpus.Files, GherkinCorpus.Configuration);

        // Act
        var result = await repository.RunCommand(
            ["verify-gherkin", "tests/corpus/dup-b.feature", "tests/corpus/sub", "tests/corpus/tags.feature", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        GherkinTestRepository.ErrorLines(result).ShouldBe(
            [Heading, .. GherkinCorpus.ExplicitPathIssues],
            "Only the named files are checked, so dup-b.feature is the first file with its Feature name.");
    }

    [Fact]
    public async Task Verify_gherkin_selects_included_feature_files_except_excluded_ones()
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["features/a.feature"] = InvalidFeature,
                ["features/skip/b.feature"] = InvalidFeature,
                ["features/c.txt"] = InvalidFeature,
                ["other/d.feature"] = InvalidFeature,
            },
            GherkinTestRepository.Configuration(include: """["features/*"]""", exclude: """["features/skip/*"]"""));

        // Act
        var result = await repository.RunCommand(["verify-gherkin", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        GherkinTestRepository.ErrorLines(result).ShouldBe(
        [
            Heading,
            "- features/a.feature: Line 1: The Feature name is required.",
            "- features/a.feature: Line 1: A Feature declaration is required.",
            "- features/a.feature: Line 1: The file has no Scenario.",
        ]);
    }

    [Fact]
    public async Task Explicit_paths_replace_the_included_patterns_but_not_the_excluded_ones()
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["features/a.feature"] = InvalidFeature,
                ["features/skip/b.feature"] = InvalidFeature,
                ["other/d.feature"] = InvalidFeature,
            },
            GherkinTestRepository.Configuration(include: """["features/*"]""", exclude: """["features/skip/*"]"""));

        // Act
        var result = await repository.RunCommand(
            ["verify-gherkin", "other", "features/skip", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("- other/d.feature: Line 1:", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("features/", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing.feature", "missing.feature names no tracked feature file.")]
    [InlineData("features/a.txt", "features/a.txt names no tracked feature file.")]
    [InlineData("feat", "feat names no tracked feature file.")]
    [InlineData("../features", "../features must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("features\\a.feature", "features\\a.feature must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    public async Task An_explicit_path_must_name_a_tracked_feature_file_or_directory(string path, string expectedMessage)
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["features/a.feature"] = ValidFeature,
                ["features/a.txt"] = ValidFeature,
            },
            GherkinTestRepository.Configuration());

        // Act
        var result = await repository.RunCommand(["verify-gherkin", path, "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: " + expectedMessage + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_gherkin_accepts_valid_feature_files_and_ignores_untracked_ones()
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["features/a.feature"] = ValidFeature },
            GherkinTestRepository.Configuration());
        repository.WriteText("features/untracked.feature", InvalidFeature);

        // Act
        var result = await repository.RunCommand(["verify-gherkin", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe("Repository Gherkin files are valid." + Environment.NewLine);
        result.StandardError.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("missing-tracked-file", "Is missing from the working tree.")]
    [InlineData("invalid-utf8", "Invalid UTF-8 at line 2, byte 15.")]
    public async Task Verify_gherkin_reports_a_feature_file_that_cannot_be_read_as_utf8(string scenario, string expectedMessage)
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["features/a.feature"] = ValidFeature },
            GherkinTestRepository.Configuration());
        if (scenario == "missing-tracked-file")
        {
            repository.Delete("features/a.feature");
        }
        else
        {
            repository.WriteBytes("features/a.feature", [.. "Feature: Valid\n    Scenario: "u8, 0xFF, (byte)'\n']);
        }

        // Act
        var result = await repository.RunCommand(["verify-gherkin", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        GherkinTestRepository.ErrorLines(result).ShouldBe([Heading, "- features/a.feature: " + expectedMessage]);
    }

    [Fact]
    public async Task Allowed_tag_patterns_match_the_whole_tag()
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["features/a.feature"] = "@a @b @ab @xa @bc @a.\n" + ValidFeature,
            },
            GherkinTestRepository.Configuration(allowedTagPatterns: """["@a|@b"]"""));

        // Act
        var result = await repository.RunCommand(["verify-gherkin", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        GherkinTestRepository.ErrorLines(result).ShouldBe(
        [
            Heading,
            "- features/a.feature: Line 1: Tag '@ab' is not allowed.",
            "- features/a.feature: Line 1: Tag '@xa' is not allowed.",
            "- features/a.feature: Line 1: Tag '@bc' is not allowed.",
            "- features/a.feature: Line 1: Tag '@a.' is not allowed.",
        ]);
    }

    [Fact]
    public async Task Verify_gherkin_fails_without_a_gherkin_section()
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["features/a.feature"] = ValidFeature },
            """{"version":1}""");

        // Act
        var result = await repository.RunCommand(["verify-gherkin", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: .config/sharedkernel-repo.json has no gherkin section." + Environment.NewLine);
    }

    [Theory]
    [InlineData("""{"version":1,"gherkin":{}}""", " is not valid at $.gherkin (line 1).")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":[],"allowedTagPatterns":[],"forbiddenTags":[],"featureNameMaxLength":1,"scenarioNameMaxLength":1,"tags":[]}}""", " is not valid at $.gherkin.tags (line 1).")]
    [InlineData("""{"version":1,"gherkin":{"include":[],"exclude":[],"allowedTagPatterns":[],"forbiddenTags":[],"featureNameMaxLength":1,"scenarioNameMaxLength":1}}""", ": $.gherkin.include must name at least one pattern.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a\\b"],"exclude":[],"allowedTagPatterns":[],"forbiddenTags":[],"featureNameMaxLength":1,"scenarioNameMaxLength":1}}""", ": $.gherkin.include[0] must be a non-empty value with '/' separators.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":["a"," "],"allowedTagPatterns":[],"forbiddenTags":[],"featureNameMaxLength":1,"scenarioNameMaxLength":1}}""", ": $.gherkin.exclude[1] must be a non-empty value with '/' separators.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":[],"allowedTagPatterns":["@a","("],"forbiddenTags":[],"featureNameMaxLength":1,"scenarioNameMaxLength":1}}""", ": $.gherkin.allowedTagPatterns[1] must be a regular expression that RegexOptions.NonBacktracking supports.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":[],"allowedTagPatterns":["(?=@a)@a"],"forbiddenTags":[],"featureNameMaxLength":1,"scenarioNameMaxLength":1}}""", ": $.gherkin.allowedTagPatterns[0] must be a regular expression that RegexOptions.NonBacktracking supports.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":[],"allowedTagPatterns":["@a)|(@b"],"forbiddenTags":[],"featureNameMaxLength":1,"scenarioNameMaxLength":1}}""", ": $.gherkin.allowedTagPatterns[0] must be a regular expression that RegexOptions.NonBacktracking supports.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":[],"allowedTagPatterns":[""],"forbiddenTags":[],"featureNameMaxLength":1,"scenarioNameMaxLength":1}}""", ": $.gherkin.allowedTagPatterns[0] must be a regular expression that RegexOptions.NonBacktracking supports.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":[],"allowedTagPatterns":[],"forbiddenTags":["skip"],"featureNameMaxLength":1,"scenarioNameMaxLength":1}}""", ": $.gherkin.forbiddenTags[0] must be a tag that starts with '@' and has no whitespace.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":[],"allowedTagPatterns":[],"forbiddenTags":["@a b"],"featureNameMaxLength":1,"scenarioNameMaxLength":1}}""", ": $.gherkin.forbiddenTags[0] must be a tag that starts with '@' and has no whitespace.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":[],"allowedTagPatterns":[],"forbiddenTags":[],"featureNameMaxLength":0,"scenarioNameMaxLength":1}}""", ": $.gherkin.featureNameMaxLength must be at least 1.")]
    [InlineData("""{"version":1,"gherkin":{"include":["a"],"exclude":[],"allowedTagPatterns":[],"forbiddenTags":[],"featureNameMaxLength":1,"scenarioNameMaxLength":-1}}""", ": $.gherkin.scenarioNameMaxLength must be at least 1.")]
    public async Task Verify_gherkin_rejects_an_invalid_gherkin_section(string configuration, string expectedMessage)
    {
        // Arrange
        using var repository = await GherkinTestRepository.Create(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["features/a.feature"] = ValidFeature },
            configuration);

        // Act
        var result = await repository.RunCommand(["verify-gherkin", "--root", repository.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldStartWith("sharedkernel-repo: .config/sharedkernel-repo.json" + expectedMessage, StringComparison.Ordinal);
        result.StandardError.ShouldEndWith(Environment.NewLine, StringComparison.Ordinal);
    }
}
