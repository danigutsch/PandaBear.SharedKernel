using SharedKernel.Repository.Tool;

namespace SharedKernel.Repository.Tests;

/// <summary>Guards project discovery and fail-closed impact selection.</summary>
public sealed class ProjectSelectorTests : IDisposable
{
    private readonly RepositoryTestScope scope = new();

    [Fact]
    public void Newly_added_solution_tests_are_selected_without_updating_provenance()
    {
        scope.AddProject("tests/New.Tests/New.Tests.csproj", """<Project><PackageReference Include="xunit.v3.mtp-v2" /></Project>""");
        var result = ProjectSelector.Select(scope.Root, ["tests/New.Tests/New.Tests.csproj"]);
        result.TestProjects.ShouldContain("tests/New.Tests/New.Tests.csproj");
    }

    [Fact]
    public void Reverse_transitive_dependents_include_complete_tests()
    {
        var result = ProjectSelector.Select(scope.Root, ["src/Core/Deleted.cs"]);
        result.BuildProjects.Length.ShouldBe(3);
        result.TestProjects.ShouldBe(["tests/Core.Tests/Core.Tests.csproj"]);
    }

    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData("unknown.file")]
    [InlineData("src/Removed/Removed.csproj")]
    public void Uncertain_and_global_changes_select_the_current_solution(string path)
    {
        var result = ProjectSelector.Select(scope.Root, [path]);
        result.BuildProjects.Length.ShouldBe(3);
        result.Reasons.ShouldContain(reason => reason.StartsWith("Full validation fallback", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_history_selects_every_current_project()
    {
        ProjectSelector.Select(scope.Root, null).BuildProjects.Length.ShouldBe(3);
    }

    [Fact]
    public void Imported_project_inputs_select_every_project()
    {
        File.WriteAllText(Path.Combine(scope.Root, "src/Core/Core.csproj"), """<Project><Import Project="custom.build" /></Project>""");
        ProjectSelector.Select(scope.Root, ["src/Dependent/Changed.cs"]).BuildProjects.Length.ShouldBe(3);
    }

    [Fact]
    public void Explicit_package_edges_select_package_consumption_tests()
    {
        var directory = Path.Combine(scope.Root, "tools/SharedKernel.Repository.Tool");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "package-consumption-edges.json"), """{"tests/Core.Tests/Core.Tests.csproj":["src/Core/Core.csproj"]}""");
        File.WriteAllText(Path.Combine(scope.Root, "tests/Core.Tests/Core.Tests.csproj"), """<Project><PackageReference Include="xunit.v3.mtp-v2" /></Project>""");
        ProjectSelector.Select(scope.Root, ["src/Core/Changed.cs"]).TestProjects.ShouldContain("tests/Core.Tests/Core.Tests.csproj");
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
