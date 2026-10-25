using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Guards each selection rule of <c>select</c> and its fail-closed fallback to full validation.</summary>
public sealed class ProjectSelectorTests : IDisposable
{
    private const string CoreTests = "tests/Core.Tests/Core.Tests.csproj";

    private readonly RepositoryTestScope scope = new();

    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData("src/Directory.Build.props")]
    [InlineData(".github/workflows/ci.yml")]
    public async Task Full_validation_patterns_select_every_test_project_before_any_other_rule(string path)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(path);

        // Act
        var selection = await scope.Select(["docs/guide.md", path, "src/Core/Changed.cs"]);

        // Assert
        selection.FullValidation.ShouldBeTrue();
        selection.Reasons.ShouldBe(["Full validation: " + path + ": matches the full-validation pattern " + (path.EndsWith(".props", StringComparison.Ordinal) ? "*.props" : ".github/*") + "."]);
        selection.BuildProjects.Length.ShouldBe(3, "Full validation builds the whole solution.");
        SelectionTestFiles.Tests(selection, "library").ShouldBe([CoreTests]);
    }

    [Theory]
    [InlineData("docs/guide.md")]
    [InlineData("README.md")]
    public async Task Documentation_changes_select_nothing_while_every_build_file_is_inert(string path)
    {
        // Act
        var selection = await scope.Select([path]);

        // Assert
        selection.FullValidation.ShouldBeFalse();
        selection.DocumentationOnly.ShouldBeTrue();
        selection.BuildRequired.ShouldBeFalse();
        selection.BuildProjects.ShouldBeEmpty();
        selection.Slices.ShouldAllSatisfy(static slice => slice.Required.ShouldBeFalse());
    }

    [Fact]
    public async Task Documentation_changes_select_every_test_project_when_a_build_file_is_not_inert()
    {
        // Act
        var selection = await scope.Select(["docs/guide.md"], "src/Core/Core.csproj uses Exec.");

        // Assert
        selection.FullValidation.ShouldBeTrue();
        selection.DocumentationOnly.ShouldBeFalse();
        selection.Reasons.ShouldHaveSingleItem().ShouldContain("src/Core/Core.csproj uses Exec.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_change_selects_its_owning_project_and_the_test_projects_of_its_reverse_transitive_dependents()
    {
        // Act
        var selection = await scope.Select(["src/Core/Deleted.cs"]);

        // Assert
        selection.FullValidation.ShouldBeFalse();
        selection.BuildRequired.ShouldBeTrue();
        selection.BuildProjects.ShouldBe(["src/Core/Core.csproj", "src/Dependent/Dependent.csproj"]);
        var test = selection.Slices.ShouldHaveSingleItem().Tests.ShouldHaveSingleItem();
        test.Project.ShouldBe(CoreTests);
        test.SelectedBy.ShouldBe(["src/Core/Deleted.cs"]);
    }

    [Fact]
    public async Task A_source_change_selects_only_its_dependent_test_projects_inside_a_larger_slice()
    {
        // Arrange
        scope.AddProject("src/Other/Other.csproj", "<Project />");
        scope.AddTestProject("tests/Other.Tests/Other.Tests.csproj", "src/Other/Other.csproj");
        scope.AddTestProject("tests/Dependent.Tests/Dependent.Tests.csproj", "src/Dependent/Dependent.csproj");
        scope.Configure(
            """[{ "name": "library", "projects": ["tests/Other.Tests/Other.Tests.csproj", "tests/Core.Tests/Core.Tests.csproj", "tests/Dependent.Tests/Dependent.Tests.csproj"] }]""",
            defaultSlice: null);

        // Act
        var selection = await scope.Select(["src/Dependent/Service.cs"]);

        // Assert
        SelectionTestFiles.Tests(selection, "library").ShouldBe([CoreTests, "tests/Dependent.Tests/Dependent.Tests.csproj"]);
        selection.BuildProjects.ShouldBe(["src/Dependent/Dependent.csproj"]);
    }

    [Fact]
    public async Task A_changed_test_project_selects_only_itself()
    {
        // Arrange
        scope.AddTestProject("tests/Other.Tests/Other.Tests.csproj", "src/Core/Core.csproj");

        // Act
        var selection = await scope.Select(["tests/Other.Tests/OtherTests.cs"]);

        // Assert
        SelectionTestFiles.Tests(selection, "library").ShouldBe(["tests/Other.Tests/Other.Tests.csproj"]);
        selection.BuildProjects.ShouldBeEmpty();
        selection.BuildRequired.ShouldBeTrue();
    }

    [Fact]
    public async Task A_shared_project_change_selects_its_dependent_test_projects_in_every_slice()
    {
        // Arrange
        scope.AddTestProject("tests/Provider.Tests/Provider.Tests.csproj", "src/Core/Core.csproj");
        scope.AddTestProject("tests/System.Tests/System.Tests.csproj", "src/Core/Core.csproj");
        scope.Configure("""
            [
              { "name": "library", "projects": [] },
              { "name": "provider", "projects": ["tests/Provider.Tests/Provider.Tests.csproj"] },
              { "name": "system", "projects": ["tests/System.Tests/System.Tests.csproj"] }
            ]
            """);

        // Act
        var selection = await scope.Select(["src/Core/Shared.cs"]);

        // Assert
        SelectionTestFiles.Tests(selection, "library").ShouldBe([CoreTests]);
        SelectionTestFiles.Tests(selection, "provider").ShouldBe(["tests/Provider.Tests/Provider.Tests.csproj"]);
        SelectionTestFiles.Tests(selection, "system").ShouldBe(["tests/System.Tests/System.Tests.csproj"]);
    }

    [Theory]
    [InlineData("unknown.file")]
    [InlineData("src/Removed/Removed.csproj")]
    [InlineData("src/Removed/Removed.cs")]
    public async Task An_unowned_path_selects_every_test_project(string path)
    {
        // Act
        var selection = await scope.Select([path]);

        // Assert
        selection.FullValidation.ShouldBeTrue();
        selection.Reasons.ShouldBe(["Full validation: " + path + ": no solution project owns it."]);
    }

    [Fact]
    public async Task Missing_history_selects_every_test_project_with_the_reason()
    {
        // Act
        var selection = await scope.Select(null);

        // Assert
        selection.FullValidation.ShouldBeTrue();
        selection.Reasons.ShouldBe(["Full validation: fixture"]);
        SelectionTestFiles.Tests(selection, "library").ShouldBe([CoreTests]);
    }

    [Theory]
    [InlineData("", """<Import Project="custom.build" />""")]
    [InlineData("http://schemas.microsoft.com/developer/msbuild/2003", """<Import Project="custom.build" />""")]
    [InlineData("", """<ProjectReference Include="$(Shared)/Shared.csproj" />""")]
    [InlineData("", """<ProjectReference Include="../Missing/Missing.csproj" />""")]
    [InlineData("", """<Compile Include="$(SharedSources)/Shared.cs" />""")]
    [InlineData("", """<Compile Include="../Core.Tests/*.cs" />""")]
    [InlineData("", """<Compile Include="../Core.Tests/Shared?.cs" />""")]
    [InlineData("", """<Compile Include="../Core.Tests/First.cs;../Core.Tests/Second.cs" />""")]
    public async Task An_unevaluated_import_reference_or_compile_item_selects_every_test_project(string xmlNamespace, string item)
    {
        // Arrange
        scope.WriteFile("src/Core/Core.csproj", $"""<Project xmlns="{xmlNamespace}">{item}</Project>""");

        // Act
        var selection = await scope.Select(["src/Dependent/Changed.cs"]);

        // Assert
        selection.FullValidation.ShouldBeTrue();
        selection.Reasons.ShouldHaveSingleItem().ShouldContain("src/Core/Core.csproj", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreadable_project_selects_every_test_project()
    {
        // Arrange
        scope.WriteFile("src/Core/Core.csproj", "<Project>");

        // Act
        var selection = await scope.Select(["src/Dependent/Changed.cs"]);

        // Assert
        selection.FullValidation.ShouldBeTrue();
        selection.Reasons.ShouldBe(["Full validation: src/Dependent/Changed.cs: changed, and src/Core/Core.csproj cannot be read."]);
    }

    [Theory]
    [InlineData("", "../Dependent/Dependent.csproj")]
    [InlineData("", "..\\Dependent\\Dependent.csproj")]
    [InlineData("http://schemas.microsoft.com/developer/msbuild/2003", "../Dependent/Dependent.csproj")]
    public async Task Namespaced_and_windows_project_references_select_transitive_test_dependents(string xmlNamespace, string include)
    {
        // Arrange
        scope.AddProject("src/Third/Third.csproj", $"""<Project xmlns="{xmlNamespace}"><ProjectReference Include="{include}" /></Project>""");
        scope.AddTestProject("tests/Third.Tests/Third.Tests.csproj", "src/Third/Third.csproj");

        // Act
        var selection = await scope.Select(["src/Core/Changed.cs"]);

        // Assert
        SelectionTestFiles.Tests(selection, "library").ShouldBe([CoreTests, "tests/Third.Tests/Third.Tests.csproj"]);
    }

    [Theory]
    [InlineData("", "../Core.Tests/SharedProcess.cs")]
    [InlineData("", "..\\Core.Tests\\SharedProcess.cs")]
    [InlineData("http://schemas.microsoft.com/developer/msbuild/2003", "../Core.Tests/SharedProcess.cs")]
    public async Task Linked_compile_changes_select_the_owner_and_exact_consumers(string xmlNamespace, string include)
    {
        // Arrange
        scope.AddProject("tests/Linked.Tests/Linked.Tests.csproj", $"""
            <Project xmlns="{xmlNamespace}">
              <PackageReference Include="xunit.v3.mtp-v2" />
              <Compile Include="{include}" Link="SharedProcess.cs" />
            </Project>
            """);

        // Act
        var linkedChange = await scope.Select(["tests/Core.Tests/SharedProcess.cs"]);
        var unrelatedChange = await scope.Select(["tests/Core.Tests/Other.cs"]);

        // Assert
        SelectionTestFiles.Tests(linkedChange, "library").ShouldBe([CoreTests, "tests/Linked.Tests/Linked.Tests.csproj"]);
        SelectionTestFiles.Tests(unrelatedChange, "library").ShouldBe([CoreTests]);
    }

    [Fact]
    public async Task A_linked_file_outside_every_project_directory_is_owned_by_the_projects_that_compile_it()
    {
        // Arrange
        scope.AddProject("tests/Linked.Tests/Linked.Tests.csproj", """
            <Project><PackageReference Include="xunit.v3.mtp-v2" /><Compile Include="../../shared/Helper.cs" Link="Helper.cs" /></Project>
            """);

        // Act
        var selection = await scope.Select(["shared/Helper.cs"]);

        // Assert
        selection.FullValidation.ShouldBeFalse();
        SelectionTestFiles.Tests(selection, "library").ShouldBe(["tests/Linked.Tests/Linked.Tests.csproj"]);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task An_uncovered_changed_project_selects_every_test_project_only_when_covering_tests_are_required(bool required, bool full)
    {
        // Arrange
        scope.AddProject("src/Uncovered/Uncovered.csproj", "<Project />");
        scope.Configure("""[{ "name": "library", "projects": [] }]""", requireCoveringTests: required);

        // Act
        var selection = await scope.Select(["src/Uncovered/Service.cs"]);

        // Assert
        selection.FullValidation.ShouldBe(full);
        selection.BuildProjects.ShouldContain("src/Uncovered/Uncovered.csproj");
        if (full)
        {
            selection.Reasons.ShouldBe(["Full validation: src/Uncovered/Uncovered.csproj: changed, and no selected test project depends on it."]);
        }
        else
        {
            selection.Slices.ShouldAllSatisfy(static slice => slice.Required.ShouldBeFalse());
        }
    }

    [Fact]
    public async Task Always_selected_test_projects_run_with_every_selective_change_and_count_as_covering()
    {
        // Arrange
        scope.AddProject("src/Scanned/Scanned.csproj", "<Project />");
        scope.AddTestProject("tests/Architecture.Tests/Architecture.Tests.csproj", "src/Scanned/Scanned.csproj");
        scope.Configure(
            """[{ "name": "library", "projects": [] }]""",
            alwaysSelected: """["tests/Architecture.Tests/Architecture.Tests.csproj"]""",
            requireCoveringTests: true);

        // Act
        var unrelated = await scope.Select(["src/Core/Changed.cs"]);
        var scanned = await scope.Select(["src/Scanned/Changed.cs"]);
        var documentation = await scope.Select(["docs/guide.md"]);

        // Assert
        unrelated.Slices[0].Tests.ShouldContain(test => test.Project == "tests/Architecture.Tests/Architecture.Tests.csproj"
            && test.SelectedBy.SequenceEqual(new[] { ProjectSelector.AlwaysSelected }));
        scanned.FullValidation.ShouldBeFalse();
        scanned.Slices[0].Tests.ShouldHaveSingleItem().SelectedBy.ShouldBe(["src/Scanned/Changed.cs", ProjectSelector.AlwaysSelected]);
        documentation.BuildRequired.ShouldBeFalse();
    }

    [Fact]
    public async Task The_default_slice_runs_the_selected_test_projects_that_no_slice_lists()
    {
        // Arrange
        scope.AddTestProject("tests/Hosted.Tests/Hosted.Tests.csproj", "src/Core/Core.csproj");
        scope.Configure("""
            [
              { "name": "library", "projects": [] },
              { "name": "hosted", "projects": ["tests/Hosted.Tests/Hosted.Tests.csproj"] }
            ]
            """);

        // Act
        var selection = await scope.Select(["src/Core/Changed.cs"]);

        // Assert
        SelectionTestFiles.Tests(selection, "library").ShouldBe([CoreTests]);
        SelectionTestFiles.Tests(selection, "hosted").ShouldBe(["tests/Hosted.Tests/Hosted.Tests.csproj"]);
    }

    private const string ConfigTests = RepositoryTestScope.ConfigTests;

    [Theory]
    [InlineData("config/settings.json")]
    [InlineData("assets/a.json")]
    public async Task Test_paths_select_only_their_test_projects_for_a_path_that_no_project_owns(string path)
    {
        // Arrange
        scope.ConfigureTestPaths();

        // Act
        var selection = await scope.Select([path]);

        // Assert
        selection.FullValidation.ShouldBeFalse();
        selection.DocumentationOnly.ShouldBeFalse();
        selection.BuildRequired.ShouldBeTrue();
        var test = selection.Slices[0].Tests.ShouldHaveSingleItem();
        test.Project.ShouldBe(ConfigTests);
        test.SelectedBy.ShouldBe([path]);
        selection.Reasons.ShouldBe([path + ": selects " + ConfigTests + " through $.selection.testPaths."]);
    }

    [Fact]
    public async Task Test_paths_add_their_test_projects_to_the_owners_and_always_selected_tests()
    {
        // Arrange
        scope.AddTestProject("tests/Architecture.Tests/Architecture.Tests.csproj");
        scope.ConfigureTestPaths("""["tests/Architecture.Tests/Architecture.Tests.csproj"]""");

        // Act
        var selection = await scope.Select(["config/settings.json", "src/Core/Changed.cs"]);

        // Assert
        SelectionTestFiles.Tests(selection, "library").ShouldBe([
            "tests/Architecture.Tests/Architecture.Tests.csproj",
            "tests/Config.Tests/Config.Tests.csproj",
            CoreTests]);
        selection.Slices[0].Tests.Where(static test => test.Project == ConfigTests).ShouldHaveSingleItem().SelectedBy.ShouldBe(["config/settings.json"]);
    }

    [Fact]
    public async Task Full_validation_and_documentation_patterns_win_over_test_paths()
    {
        // Arrange
        scope.AddTestProject(ConfigTests);
        scope.Configure(
            """[{ "name": "library", "projects": [] }]""",
            testPaths: """[{ "paths": ["docs/*", ".github/*"], "tests": ["tests/Config.Tests/Config.Tests.csproj"] }]""");

        // Act
        var documentation = await scope.Select(["docs/guide.md"]);
        var full = await scope.Select([".github/workflows/ci.yml"]);

        // Assert
        documentation.DocumentationOnly.ShouldBeTrue();
        documentation.Slices[0].Tests.ShouldBeEmpty();
        full.FullValidation.ShouldBeTrue();
    }

    [Fact]
    public async Task A_path_that_no_test_path_matches_and_no_project_owns_still_selects_every_test_project()
    {
        // Arrange
        scope.ConfigureTestPaths();

        // Act
        var selection = await scope.Select(["other/settings.json"]);

        // Assert
        selection.FullValidation.ShouldBeTrue();
        selection.Reasons.ShouldHaveSingleItem().ShouldContain("no solution project owns it", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""[{ "paths": ["config/*"], "tests": ["tests/Missing/Missing.csproj"] }]""", "$.selection.testPaths[0].tests[0] names tests/Missing/Missing.csproj, which is not a project of SharedKernel.slnx.")]
    [InlineData("""[{ "paths": ["config/*"], "tests": ["src/Core/Core.csproj"] }]""", "$.selection.testPaths[0].tests[0] names src/Core/Core.csproj, which is not a test project.")]
    public async Task Test_path_entries_must_name_solution_test_projects(string testPaths, string error)
    {
        // Arrange
        scope.Configure("""[{ "name": "library", "projects": [] }]""", testPaths: testPaths);

        // Act
        Func<Task> select = () => scope.Select(["config/settings.json"]);

        // Assert
        (await select.ShouldThrow<InvalidOperationException>()).Message.ShouldBe(".config/sharedkernel-ci.json: " + error);
    }

    [Theory]
    [InlineData("""[{ "name": "library", "projects": ["tests/Missing/Missing.csproj"] }]""", "$.slices[0].projects[0] names tests/Missing/Missing.csproj, which is not a project of SharedKernel.slnx.")]
    [InlineData("""[{ "name": "library", "projects": ["src/Core/Core.csproj"] }]""", "$.slices[0].projects[0] names src/Core/Core.csproj, which is not a test project.")]
    [InlineData("""[{ "name": "library", "projects": ["tests/Core.Tests/Core.Tests.csproj"] }, { "name": "other", "projects": ["tests/Core.Tests/Core.Tests.csproj"] }]""",
        "$.slices[1].projects[0] names tests/Core.Tests/Core.Tests.csproj, which slice library already lists; a test project belongs to one slice.")]
    [InlineData("""[{ "name": "other", "projects": ["tests/Core.Tests/Core.Tests.csproj"] }, { "name": "library", "projects": [] }]""", null)]
    public async Task Slice_entries_must_be_solution_test_projects_in_exactly_one_slice(string slices, string? error)
    {
        // Arrange
        scope.Configure(slices);

        // Act
        Func<Task> select = () => scope.Select(["src/Core/Changed.cs"]);

        // Assert
        if (error is null)
        {
            (await scope.Select(["src/Core/Changed.cs"])).Slices[0].Tests.ShouldHaveSingleItem();
        }
        else
        {
            (await select.ShouldThrow<InvalidOperationException>()).Message.ShouldBe(".config/sharedkernel-ci.json: " + error);
        }
    }

    [Theory]
    [InlineData("tests/Missing/Host.csproj", "$.slices[0].service.project names tests/Missing/Host.csproj, which is not a project of SharedKernel.slnx.")]
    [InlineData("tests/Core.Tests/Core.Tests.csproj", "$.slices[0].service.project names tests/Core.Tests/Core.Tests.csproj, which is a test project; a service is not.")]
    [InlineData("src/Core/Core.csproj", null)]
    public async Task A_service_must_be_a_solution_project_that_is_not_a_test_project(string project, string? error)
    {
        // Arrange
        scope.Configure($$"""
            [{ "name": "library", "projects": [], "service": { "project": "{{project}}", "readyFileVariable": "APP_READY_FILE", "valueVariable": "APP_VALUE" } }]
            """);

        // Act
        Func<Task> select = () => scope.Select(["src/Core/Changed.cs"]);

        // Assert
        if (error is null)
        {
            (await scope.Select(["src/Core/Changed.cs"])).Slices[0].Tests.ShouldHaveSingleItem();
        }
        else
        {
            (await select.ShouldThrow<InvalidOperationException>()).Message.ShouldBe(".config/sharedkernel-ci.json: " + error);
        }
    }

    [Fact]
    public async Task Without_a_default_slice_every_solution_test_project_must_be_in_a_slice()
    {
        // Arrange
        scope.AddTestProject("tests/Unlisted.Tests/Unlisted.Tests.csproj");
        scope.Configure("""[{ "name": "library", "projects": ["tests/Core.Tests/Core.Tests.csproj"] }]""", defaultSlice: null);

        // Act
        Func<Task> select = () => scope.Select(["src/Core/Changed.cs"]);
        var exception = await select.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe(
            ".config/sharedkernel-ci.json: $.slices must list every test project of SharedKernel.slnx when $.defaultSlice is not set. Unlisted: tests/Unlisted.Tests/Unlisted.Tests.csproj.");
    }

    [Fact]
    public async Task An_empty_diff_selects_nothing_and_counts_as_documentation_only()
    {
        // Act
        var selection = await scope.Select([]);

        // Assert
        selection.FullValidation.ShouldBeFalse();
        selection.BuildRequired.ShouldBeFalse();
        selection.DocumentationOnly.ShouldBeTrue();
        selection.Reasons.ShouldBe(["No path changed between the base and HEAD."]);
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
