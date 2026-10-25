using System.Xml.Linq;
using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>Guards this repository's <c>.config/sharedkernel-ci.json</c> against selections that miss a package or a service, and against hosted tests without one.</summary>
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
    public async Task Every_aspire_app_host_test_project_runs_in_a_slice_with_a_service()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);

        // Act
        // ponytail: membership only. A hosted project that does not borrow the slice's server still passes, and so does a second
        // kind of hosted project (Redis, ClamAV) in the postgres slice, whose containers would then start beside the shared
        // server. All three hosted projects use PostgreSQL today; require a reference to SharedKernel.Npgsql.Testing in a
        // served slice, the code that reads the variable, when a project of another kind arrives.
        var hosted = graph.Tests
            .Where(project => XDocument.Load(Path.Combine(root, project)).Root?.Attribute("Sdk")?.Value.StartsWith("Aspire.AppHost.Sdk", StringComparison.Ordinal) == true)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var unserved = hosted
            .Where(project => configuration.Slices.SingleOrDefault(slice => slice.Projects.Contains(project, StringComparer.Ordinal)) is not { Service: not null } slice
                || slice.Name == configuration.DefaultSlice)
            .ToArray();

        // Assert
        hosted.ShouldNotBeEmpty();
        unserved.ShouldBeEmpty("A hosted test project starts its own containers unless a service supplies the server that its slice shares, so it never runs in the default slice or in a slice without a service.");
    }

    [Fact]
    public async Task Every_slice_service_is_a_project_that_is_not_a_test_project_and_hands_over_a_valid_distinct_variable()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);

        // Act
        var services = configuration.Slices.Where(static slice => slice.Service is not null).Select(static slice => (slice.Name, Service: slice.Service!)).ToArray();
        var invalid = services
            .SelectMany(static slice => new[] { slice.Service.ReadyFileVariable, slice.Service.ValueVariable })
            .Where(static variable => variable.Length == 0 || char.IsAsciiDigit(variable[0]) || !variable.All(static character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            .ToArray();

        // Assert
        services.ShouldNotBeEmpty();
        services.ShouldAllSatisfy(slice =>
        {
            graph.Tests.ShouldNotContain(slice.Service.Project, "A service is a host, not a test project.");
            slice.Service.ReadyFileVariable.ShouldNotBe(slice.Service.ValueVariable, "The service and the tests read different variables.");
        });
        invalid.ShouldBeEmpty("An environment variable name is letters, digits, and '_', and does not start with a digit.");
        services.Select(static slice => slice.Service.ValueVariable).ShouldBe(
            [.. services.Select(static slice => slice.Service.ValueVariable).Distinct(StringComparer.OrdinalIgnoreCase)],
            "Slices that run together share one test environment, so they cannot hand over the same variable.");
    }

    [Fact]
    public async Task The_postgres_slice_shares_one_server_through_the_variables_that_the_test_assemblies_and_the_host_read()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();

        // Act
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);

        // Assert
        var postgres = configuration.Slices.ShouldHaveSingleItem(static slice => slice.Name == "postgres");
        postgres.Projects.ShouldBe(
        [
            "tests/SharedKernel.EntityFrameworkCore.Tests/SharedKernel.EntityFrameworkCore.Tests.csproj",
            "tests/SharedKernel.EventSourcing.Npgsql.Tests/SharedKernel.EventSourcing.Npgsql.Tests.csproj",
            "tests/SharedKernel.Npgsql.Tests/SharedKernel.Npgsql.Tests.csproj",
        ]);
        postgres.Service.ShouldBe(
            new ServiceConfiguration(
                "tests/SharedKernel.Npgsql.Testing.Host/SharedKernel.Npgsql.Testing.Host.csproj",
                "SHAREDKERNEL_TESTING_POSTGRESQL_READY_FILE",
                "SHAREDKERNEL_TESTING_POSTGRESQL_CONNECTION_STRING",
                StopTimeoutSeconds: 180),
            "The names are the contract between the CI tool, the owner host, and the test assemblies, and the stop timeout outlasts the two minutes that the container runtime may take to remove the server, because the host removes its container while it exits and a kill earlier would interrupt that.");
    }

    [Fact]
    public async Task A_change_to_a_slice_service_or_to_the_tool_code_that_runs_it_selects_every_test_project_of_its_slice()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);
        var served = configuration.Slices.Where(static slice => slice.Service is not null).ToArray();
        string[] protocol = ["tools/SharedKernel.CI.Tool/ServiceConfiguration.cs", "tools/SharedKernel.CI.Tool/ServiceHost.cs"];

        // Act
        var unselected = served
            .SelectMany(slice =>
            {
                string[] changes = [slice.Service!.Project[..(slice.Service.Project.LastIndexOf('/') + 1)] + "Source.cs", .. protocol];
                return changes.Select(changed =>
                {
                    var plan = ProjectSelector.Select(configuration, graph, [changed], null, null);
                    var selected = plan.Slices.SelectMany(static selection => selection.Tests).Select(static test => test.Project).ToArray();
                    return (Changed: changed, Projects: plan.FullValidation ? [.. slice.Projects] : slice.Projects.Except(selected, StringComparer.Ordinal).ToArray());
                });
            })
            .ToArray();

        // Assert
        served.ShouldNotBeEmpty();
        protocol.ShouldAllSatisfy(file => File.Exists(Path.Combine(root, file)).ShouldBeTrue(file + " runs a service, so the rule that names it must not outlive it."));
        unselected.ShouldAllSatisfy(
            static change => change.Projects.ShouldBeEmpty(
                change.Changed + " must select every test project of the slice: most of them do not reference the service or the tool, and only the real tool, host, and tests together prove the start, hand-over, and stop of the service."));
    }

    [Fact]
    public async Task A_change_to_the_service_protocol_of_the_tool_still_selects_the_tests_of_the_tool()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);
        const string toolTests = "tests/SharedKernel.Repository.Tests/SharedKernel.Repository.Tests.csproj";
        string[] protocol = ["tools/SharedKernel.CI.Tool/ServiceConfiguration.cs", "tools/SharedKernel.CI.Tool/ServiceHost.cs"];

        // Act
        var unselected = protocol
            .Where(file => !ProjectSelector.Select(configuration, graph, [file], null, null).Slices
                .SelectMany(static selection => selection.Tests)
                .Any(test => test.Project == toolTests))
            .ToArray();

        // Assert
        unselected.ShouldBeEmpty("A selection.testPaths rule selects the projects it names and no others, so the rule for the protocol must name the tests of the tool as well.");
    }

    [Fact]
    public async Task Every_test_project_collects_coverage_for_run_coverage()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);

        // Act
        var uncollected = graph.Tests
            .Where(project => !XDocument.Load(Path.Combine(root, project)).Descendants()
                .Any(static element => element.Name.LocalName == "PackageReference" && (string?)element.Attribute("Include") == "coverlet.MTP"))
            .ToArray();

        // Assert
        graph.Tests.ShouldNotBeEmpty();
        uncollected.ShouldBeEmpty("run --coverage fails when a selected test project writes no Cobertura report.");
    }
}
