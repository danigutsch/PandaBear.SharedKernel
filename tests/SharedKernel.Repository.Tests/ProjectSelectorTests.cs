using PandaBear.Repository.Tool;

namespace PandaBear.Repository.Tests;

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

    [Theory]
    [InlineData("")]
    [InlineData("http://schemas.microsoft.com/developer/msbuild/2003")]
    public void Imported_project_inputs_select_every_project(string xmlNamespace)
    {
        File.WriteAllText(Path.Combine(scope.Root, "src/Core/Core.csproj"), $"""<Project xmlns="{xmlNamespace}"><Import Project="custom.build" /></Project>""");
        ProjectSelector.Select(scope.Root, ["src/Dependent/Changed.cs"]).BuildProjects.Length.ShouldBe(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://schemas.microsoft.com/developer/msbuild/2003")]
    public void Namespaced_project_references_select_transitive_test_dependents(string xmlNamespace)
    {
        File.WriteAllText(
            Path.Combine(scope.Root, "src/Dependent/Dependent.csproj"),
            $"""<Project xmlns="{xmlNamespace}"><ProjectReference Include="../Core/Core.csproj" /></Project>""");
        File.WriteAllText(
            Path.Combine(scope.Root, "tests/Core.Tests/Core.Tests.csproj"),
            $"""<Project xmlns="{xmlNamespace}"><PackageReference Include="xunit.v3.mtp-v2" /><ProjectReference Include="../../src/Dependent/Dependent.csproj" /></Project>""");

        var result = ProjectSelector.Select(scope.Root, ["src/Core/Changed.cs"]);

        result.BuildProjects.Length.ShouldBe(3);
        result.TestProjects.ShouldBe(["tests/Core.Tests/Core.Tests.csproj"]);
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

    [Theory]
    [InlineData("", "../Core.Tests/SharedProcess.cs")]
    [InlineData("", "..\\Core.Tests\\SharedProcess.cs")]
    [InlineData("http://schemas.microsoft.com/developer/msbuild/2003", "../Core.Tests/SharedProcess.cs")]
    [InlineData("http://schemas.microsoft.com/developer/msbuild/2003", "..\\Core.Tests\\SharedProcess.cs")]
    public void Linked_compile_changes_select_the_owner_and_exact_consumers(string xmlNamespace, string include)
    {
        // Arrange
        scope.AddProject("tests/Linked.Tests/Linked.Tests.csproj", $"""
            <Project xmlns="{xmlNamespace}">
              <PackageReference Include="xunit.v3.mtp-v2" />
              <Compile Include="{include}" Link="SharedProcess.cs" />
            </Project>
            """);

        // Act
        var linkedChange = ProjectSelector.Select(scope.Root, ["tests/Core.Tests/SharedProcess.cs"]);
        var unrelatedChange = ProjectSelector.Select(scope.Root, ["tests/Core.Tests/Other.cs"]);

        // Assert
        linkedChange.TestProjects.ShouldBe(["tests/Core.Tests/Core.Tests.csproj", "tests/Linked.Tests/Linked.Tests.csproj"]);
        unrelatedChange.TestProjects.ShouldBe(["tests/Core.Tests/Core.Tests.csproj"]);
    }

    [Theory]
    [InlineData("$(SharedSources)/Shared.cs")]
    [InlineData("../Core.Tests/*.cs")]
    [InlineData("../Core.Tests/Shared?.cs")]
    [InlineData("../Core.Tests/First.cs;../Core.Tests/Second.cs")]
    public void Unevaluated_compile_inputs_select_the_current_solution(string include)
    {
        // Arrange
        scope.AddProject("tests/Linked.Tests/Linked.Tests.csproj", $"""<Project><Compile Include="{include}" /></Project>""");

        // Act
        var result = ProjectSelector.Select(scope.Root, ["tests/Core.Tests/SharedProcess.cs"]);

        // Assert
        result.BuildProjects.Length.ShouldBe(4);
        result.Reasons.ShouldContain(reason => reason.StartsWith("Full validation fallback", StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
