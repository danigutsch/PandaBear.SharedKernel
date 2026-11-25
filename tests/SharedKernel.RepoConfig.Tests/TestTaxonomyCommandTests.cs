namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises repository test taxonomy through the command line.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class TestTaxonomyCommandTests
{
    [Fact]
    public async Task Accepts_an_explicit_canonical_suite_and_tracked_seam_register()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(0);
        output.ToString().ShouldContain("Test taxonomy is valid.", StringComparison.Ordinal);
        error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Reports_unregistered_projects_even_when_their_names_look_canonical()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create("", ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("tests/Widget.Tests.Unit/Widget.Tests.Unit.csproj: Declare this test project", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_invalid_seam_and_unregistered_sut_and_absent_register()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var entry = TestTaxonomyTestRepository.CanonicalSuite
            .Replace("src/Widget/Widget.csproj", "src/Missing/Missing.csproj", StringComparison.Ordinal)
            .Replace("\"Unit\"", "\"Behavior\"", StringComparison.Ordinal)
            .Replace("tests/SEAMS.md", "tests/Missing.md", StringComparison.Ordinal);
        using var repository = await TestTaxonomyTestRepository.Create(entry, ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy", "--report"], output, error, repository.RootPath, ct);
        var diagnostics = error.ToString();

        // Assert
        result.ShouldBe(0);
        diagnostics.ShouldContain("test type 'Behavior' is unsupported", StringComparison.Ordinal);
        diagnostics.ShouldContain("SUT 'src/Missing/Missing.csproj' is not registered", StringComparison.Ordinal);
        diagnostics.ShouldContain("seam register 'tests/Missing.md' is not a tracked Markdown file", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Widget.LegacyTests", "", 1)]
    [InlineData("Widget.LegacyTests", ", \"migrationException\": \"Retains the legacy runner name until its callers move.\"", 0)]
    public async Task Requires_a_migration_reason_for_legacy_names(string projectName, string exception, int expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var entry = TestTaxonomyTestRepository.CanonicalSuite
            .Replace("Widget.Tests.Unit", projectName, StringComparison.Ordinal)
            .Replace("\n}", exception + "\n}", StringComparison.Ordinal);
        using var repository = await TestTaxonomyTestRepository.Create(entry, ct, projectName);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(expected);
        if (expected != 0)
        {
            error.ToString().ShouldContain("Rename to 'Widget.Tests.Unit'", StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Reports_missing_production_coverage_entries_and_missing_required_types()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create("", ct);
        repository.WriteText("src/Other/Other.csproj", "<Project />");
        await repository.Stage("src/Other/Other.csproj", ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, ct);
        var diagnostics = error.ToString();

        // Assert
        result.ShouldBe(1);
        diagnostics.ShouldContain("src/Other/Other.csproj: Register this production SUT", StringComparison.Ordinal);
        diagnostics.ShouldContain("src/Widget/Widget.csproj: Declare a suite for required test type 'Unit'.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_in_report_mode_when_the_inventory_is_untracked()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy", "--report"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(1);
        error.ToString().ShouldContain("Track the repository test taxonomy", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"version\": 1", "\"version\": 2", "must be a version 1 inventory", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": []", "must declare separate, root-relative", 1)]
    [InlineData("\"testRoots\": [\"tests\"]", "\"testRoots\": []", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\" \"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"src\\\\child\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"../src\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"src/./child\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"src//child\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"sutRoots\": [\"src\"]", "\"sutRoots\": [\"tests/child\"]", "must declare separate, root-relative", 1)]
    [InlineData("\"suts\": [", "\"suts\": [null,", "Remove null entries", 0)]
    [InlineData("\"suites\": [", "\"suites\": [null,", "Remove null entries", 0)]
    [InlineData("\"path\": \"src/Widget/Widget.csproj\"", "\"path\": \"src/Unknown.csproj\"", "production SUT 'src/Unknown.csproj'", 0)]
    [InlineData("\"directOwner\": \"Widget\"", "\"directOwner\": \"\"", "Declare the direct owner", 0)]
    [InlineData("\"requiredTypes\": [\"Unit\"]", "\"requiredTypes\": [\"Behavior\"]", "Required test type 'Behavior' is unsupported", 0)]
    [InlineData("\"type\": \"Unit\"", "\"type\": \"Behavior\"", "test type 'Behavior' is unsupported", 0)]
    [InlineData("\"seams\": [", "\"seams\": [null,", "Declare at least one seam", 0)]
    [InlineData("tests/SEAMS.md", "tests/SEAMS.txt", "is not a tracked Markdown file", 0)]
    [InlineData("\"dependency\": \"in-process\"", "\"dependency\": \"\"", "Declare a dependency, CI lane, and at least one capability", 0)]
    [InlineData("\"lane\": \"fast\"", "\"lane\": \"\"", "Declare a dependency, CI lane, and at least one capability", 0)]
    [InlineData("[\"example\"]", "[]", "Declare a dependency, CI lane, and at least one capability", 0)]
    [InlineData("[\"example\"]", "[\" \"]", "Declare a dependency, CI lane, and at least one capability", 0)]
    [InlineData("\"supportProjects\": []", "\"supportProjects\": [\"tests/Support.csproj\"]", "must be a tracked project", 0)]
    [InlineData("\"supportProjects\": []", "\"supportProjects\": [\"tests/SEAMS.md\"]", "must be a tracked project", 0)]
    [InlineData("\"supportProjects\": []", "\"supportProjects\": [\"src/Widget/Widget.csproj\"]", "must be a tracked project", 0)]
    [InlineData(
        "\"supportProjects\": []",
        "\"supportProjects\": [\"tests/Widget.Tests.Unit/Widget.Tests.Unit.csproj\"]",
        "Remove the duplicate test support project declaration",
        0)]
    public async Task Reports_inventory_shape_and_ownership_problems(string original, string replacement, string diagnostic, int expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = await TestTaxonomyTestRepository.Create(TestTaxonomyTestRepository.CanonicalSuite, ct);
        const string inventoryPath = ".config/test-taxonomy.json";
        var inventory = await File.ReadAllTextAsync(Path.Combine(repository.RootPath, inventoryPath), ct);
        repository.WriteText(inventoryPath, inventory.Replace(original, replacement, StringComparison.Ordinal));
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy", "--report"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(expected);
        error.ToString().ShouldContain(diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_seams_need_a_single_declaration_and_a_valid_project_shape()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var suite = TestTaxonomyTestRepository.CanonicalSuite.Replace(
            "\"seams\": [",
            """
            "seams": [
              {"sut":"src/Widget/Widget.csproj","type":"Unit","dependency":"in-process","capabilities":["duplicate"],"lane":"fast"},
            """,
            StringComparison.Ordinal);
        using var repository = await TestTaxonomyTestRepository.Create(suite, ct);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var result = await RepoConfigToolRunner.Run(["verify-test-taxonomy"], output, error, repository.RootPath, ct);

        // Assert
        result.ShouldBe(1);
        var diagnostics = error.ToString();
        diagnostics.ShouldContain("Merge duplicate seam declarations", StringComparison.Ordinal);
        diagnostics.ShouldContain("Split the project by declared SUT and test type", StringComparison.Ordinal);
    }
}
