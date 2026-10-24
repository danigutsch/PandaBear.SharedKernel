using System.Xml.Linq;
using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>Guards this repository's <c>.config/sharedkernel-ci.json</c> against selections that miss a package or isolate too little.</summary>
public sealed class CiConfigurationConventionTests
{
    private const string StyleAnalyzerTests = "tests/SharedKernel.Style.Analyzers.Tests/SharedKernel.Style.Analyzers.Tests.csproj";

    [Fact]
    public async Task Every_packable_project_change_requires_a_build_and_so_the_candidate_job()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);

        // Act
        var packable = graph.Projects.Where(project =>
        {
            var elements = XDocument.Load(Path.Combine(root, project)).Descendants().ToArray();
            return elements.Any(static element => element.Name.LocalName == "PackageId" && element.Value.StartsWith("PandaBear.SharedKernel.", StringComparison.Ordinal))
                && !elements.Any(static element => element.Name.LocalName == "IsPackable" && element.Value.Trim() == "false");
        }).ToArray();
        var unbuilt = packable
            .Where(project => !ProjectSelector.Select(configuration, graph, [project[..(project.LastIndexOf('/') + 1)] + "Source.cs"], null, null).BuildRequired)
            .ToArray();

        // Assert
        packable.ShouldNotBeEmpty();
        unbuilt.ShouldBeEmpty("build_required starts the candidate job, which packs and consumes every package whenever a packable project changes.");
    }

    [Fact]
    public async Task Full_validation_covers_the_style_analyzer_tests()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();

        // Act
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);

        // Assert
        ProjectSelector.Match(configuration.Selection.FullValidation, StyleAnalyzerTests).ShouldNotBeNull();
        ProjectSelector.Match(configuration.Selection.FullValidation, StyleAnalyzerTests[..(StyleAnalyzerTests.LastIndexOf('/') + 1)] + "Source.cs")
            .ShouldNotBeNull("The style analyzer tests guard the governed literals of every library, so their change runs every test project.");
    }

    [Fact]
    public async Task Every_aspire_app_host_test_project_runs_alone_in_its_slice()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);

        // Act
        var hosted = graph.Tests
            .Where(project => XDocument.Load(Path.Combine(root, project)).Root?.Attribute("Sdk")?.Value.StartsWith("Aspire.AppHost.Sdk", StringComparison.Ordinal) == true)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var shared = hosted.Where(project => configuration.Slices.SingleOrDefault(slice => slice.Projects.Contains(project, StringComparer.Ordinal))?.Projects is not [_])
            .ToArray();

        // Assert
        hosted.ShouldNotBeEmpty();
        shared.ShouldBeEmpty("Each hosted test project starts its own containers, so it runs in a job of its own.");
    }
}
