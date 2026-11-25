using System.Xml.Linq;

namespace SharedKernel.Repository.Tests.Conventions;

/// <summary>
/// Guards this repository's <c>.config/sharedkernel-ci.json</c> against selections that miss a package or a service, and against tests that
/// borrow a shared service outside the slice whose service supplies it or that start a second instance of one in CI.
/// </summary>
public sealed class CiConfigurationConventionTests
{
    private const string StyleAnalyzerTests = "tests/SharedKernel.Style.Analyzers.Tests/SharedKernel.Style.Analyzers.Tests.csproj";

    [Fact]
    public async Task Every_packable_project_change_requires_a_build_and_so_the_candidate_job()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
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
            .Where(project => !ProjectSelector.Select(configuration, graph, [project[..(project.AsSpan().LastIndexOf('/') + 1)] + "Source.cs"], null, null).BuildRequired)
            .ToArray();

        // Assert
        packable.ShouldNotBeEmpty();
        unbuilt.ShouldBeEmpty("build_required starts the candidate job, which packs and consumes every package whenever a packable project changes.");
    }

    [Theory]
    [InlineData(StyleAnalyzerTests, true)]
    [InlineData("tests/SharedKernel.Style.Analyzers.Tests/GovernedLiteralRepositoryTests.cs", true)]
    [InlineData("src/SharedKernel/SharedKernel.Style.Analyzers/StyleAnalyzer.cs", true)]
    [InlineData("src/SharedKernel/SharedKernel.Style.Analyzers/GovernedLiterals.txt", true)]
    [InlineData("src/SharedKernel/SharedKernel.Style.CodeFixes/StyleCodeFixProvider.cs", true)]
    [InlineData("src/SharedKernel/SharedKernel.Testing.Analyzers/TestingAnalyzer.cs", true)]
    [InlineData("src/SharedKernel/SharedKernel.Testing.CodeFixes/TestingCodeFixProvider.cs", true)]
    [InlineData("src/SharedKernel/SharedKernel.PageSections/PageSection.cs", false)]
    public async Task Global_compiler_inputs_select_full_validation_and_ordinary_sources_stay_selective(string path, bool fullValidation)
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);

        // Act
        var plan = ProjectSelector.Select(configuration, graph, [path], null, null);

        // Assert
        plan.FullValidation.ShouldBe(fullValidation, "Inherited build files apply the analyzers and literal catalog outside the explicit project graph.");
        plan.BuildRequired.ShouldBeTrue();
    }

    [Fact]
    public async Task Every_variable_that_a_slice_service_hands_over_is_read_by_a_helper_that_is_neither_a_test_nor_a_service()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);

        // Act
        var helpers = SharedServiceHelpers.Find(root, configuration, graph);
        var unread = configuration.Slices
            .SelectMany(static slice => slice.Service?.ValueVariables ?? [])
            .Where(variable => !helpers.ContainsKey(variable))
            .ToArray();

        // Assert
        helpers.ShouldNotBeEmpty();
        unread.ShouldBeEmpty("A variable that no helper reads is handed to no test, so the service starts something that nothing borrows.");
    }

    [Fact]
    public async Task Every_test_project_that_borrows_a_shared_service_runs_in_the_slice_whose_service_supplies_it()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);
        var helpers = SharedServiceHelpers.Find(root, configuration, graph);

        // Act
        var borrowing = graph.Tests
            .Order(StringComparer.Ordinal)
            .Select(project => (Project: project, Variables: SharedServiceHelpers.Borrowed(root, project, helpers)))
            .Where(static test => test.Variables.Length > 0)
            .ToArray();
        var unserved = borrowing
            .SelectMany(test => test.Variables
                .Where(variable => configuration.Slices.SingleOrDefault(slice => slice.Projects.Contains(test.Project, StringComparer.Ordinal))?.Service?.ValueVariables
                    .Contains(variable, StringComparer.Ordinal) != true)
                .Select(variable => test.Project + " borrows " + variable))
            .ToArray();

        // Assert
        borrowing.ShouldNotBeEmpty();
        unserved.ShouldBeEmpty(
            "A test project that references the helper that reads a variable of a slice service runs in that slice; elsewhere the variable is not set, and each assembly starts an instance of its own.");
    }

    [Fact]
    public async Task Every_hosted_test_project_runs_in_a_slice_with_a_service_and_borrows_from_it()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);
        var helpers = SharedServiceHelpers.Find(root, configuration, graph);

        // Act
        var hosted = graph.Tests
            .Where(project => XDocument.Load(Path.Combine(root, project)).Root?.Attribute("Sdk")?.Value.StartsWith("Aspire.AppHost.Sdk", StringComparison.Ordinal) == true)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var unserved = hosted
            .Where(project => configuration.Slices.SingleOrDefault(slice => slice.Projects.Contains(project, StringComparer.Ordinal)) is not { Service: { } service } slice
                || slice.Name == configuration.DefaultSlice
                || !SharedServiceHelpers.Borrowed(root, project, helpers).Any(variable => service.ValueVariables.Contains(variable, StringComparer.Ordinal)))
            .ToArray();

        // Assert
        hosted.ShouldNotBeEmpty();
        unserved.ShouldBeEmpty(
            "A hosted test project starts containers on its own unless it borrows them. Every service that tests need runs once per CI run in the host of a slice service, so a hosted test project runs in that slice and borrows from it instead of starting a second instance.");
    }

    [Fact]
    public async Task Every_slice_service_is_a_project_that_is_not_a_test_project_and_hands_over_a_valid_distinct_variable()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);

        // Act
        var services = configuration.Slices.Where(static slice => slice.Service is not null).Select(static slice => (slice.Name, Service: slice.Service!)).ToArray();
        var invalid = services
            .SelectMany(static slice => slice.Service.ValueVariables.Prepend(slice.Service.ReadyFileVariable))
            .Where(static variable => variable.Length == 0 || char.IsAsciiDigit(variable[0]) || !variable.All(static character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            .ToArray();

        // Assert
        services.ShouldNotBeEmpty();
        services.ShouldAllSatisfy(slice =>
        {
            graph.Tests.ShouldNotContain(slice.Service.Project, "A service is a host, not a test project.");
            slice.Service.ValueVariables.ShouldNotContain(slice.Service.ReadyFileVariable, "The service and the tests read different variables.");
        });
        invalid.ShouldBeEmpty("An environment variable name is letters, digits, and '_', and does not start with a digit.");
        services.SelectMany(static slice => slice.Service.ValueVariables).ShouldBe(
            [.. services.SelectMany(static slice => slice.Service.ValueVariables).Distinct(StringComparer.OrdinalIgnoreCase)],
            "Slices that run together share one test environment, so they cannot hand over the same variable.");
    }

    [Fact]
    public async Task The_services_slice_shares_one_instance_of_each_service_through_the_variables_that_the_test_assemblies_and_the_host_read()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();

        // Act
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);

        // Assert
        var services = configuration.Slices.ShouldHaveSingleItem(static slice => slice.Service is not null);
        services.Name.ShouldBe("services");
        services.Projects.ShouldBe(
        [
            "tests/SharedKernel.Aspire.Hosting.Umami.Tests/SharedKernel.Aspire.Hosting.Umami.Tests.csproj",
            "tests/SharedKernel.EntityFrameworkCore.Tests/SharedKernel.EntityFrameworkCore.Tests.csproj",
            "tests/SharedKernel.EventSourcing.Npgsql.Tests/SharedKernel.EventSourcing.Npgsql.Tests.csproj",
            "tests/SharedKernel.Npgsql.Tests/SharedKernel.Npgsql.Tests.csproj",
        ]);
        var service = services.Service.ShouldNotBeNull();
        service.Project.ShouldBe("tests/SharedKernel.TestServices.Host/SharedKernel.TestServices.Host.csproj");
        service.ReadyFileVariable.ShouldBe("SHAREDKERNEL_TESTING_SERVICES_READY_FILE", "The names are the contract between the CI tool, the host, and the test assemblies.");
        service.ValueVariables.ShouldBe(
            ["SHAREDKERNEL_TESTING_POSTGRESQL_CONNECTION_STRING", "SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING"],
            "The names are the contract between the CI tool, the host, and the test assemblies.");
        service.ReadyTimeoutSeconds.ShouldBe(
            600,
            "The host hands over PostgreSQL after up to 90 seconds and Umami after up to five more minutes, so the tool must not give up before the host reports an Umami that did not start.");
        service.StopTimeoutSeconds.ShouldBe(
            180,
            "The stop timeout outlasts the two minutes that the container runtime may take to remove the containers, because the host removes them while it exits and a kill earlier would interrupt that.");
    }

    [Fact]
    public async Task A_change_to_a_slice_service_or_to_the_tool_code_that_runs_it_selects_every_test_project_of_its_slice()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);
        var served = configuration.Slices.Where(static slice => slice.Service is not null).ToArray();
        string[] protocol = ["src/SharedKernel/SharedKernel.CI/ServiceConfiguration.cs", "src/SharedKernel/SharedKernel.CI/ServiceHost.cs"];

        // Act
        var unselected = served
            .SelectMany(slice =>
            {
                string[] changes = [slice.Service!.Project[..(slice.Service.Project.AsSpan().LastIndexOf('/') + 1)] + "Source.cs", .. protocol];
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
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var configuration = await CiConfigurationFile.Read(root, TestContext.Current.CancellationToken);
        var graph = SolutionGraph.Load(root, configuration.Solution);
        const string toolTests = "tests/SharedKernel.Repository.Tests/SharedKernel.Repository.Tests.csproj";
        string[] protocol = ["src/SharedKernel/SharedKernel.CI/ServiceConfiguration.cs", "src/SharedKernel/SharedKernel.CI/ServiceHost.cs"];

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
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
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
