namespace PandaBear.Blazor.Analyzers.Tests;

/// <summary>Tests that the analyzer reports route parameters read in OnInitialized (SKBLZ004) only where intended.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ComponentStateCapability)]
public sealed class StaleParameterAnalyzerTests
{
    private const string IdParameter = "[Parameter] public Guid? Id { get; set; }";
    private const string IdRoute = "[Route(\"/items/{Id:guid}\")]";

    [Fact]
    public async Task A_route_parameter_read_in_async_initialization_is_reported()
    {
        // Arrange
        var source = StaleParameterSources.Page(IdRoute, IdParameter, StaleParameterSources.OnInitializedAsync("var id = Id;"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(BlazorDiagnosticIds.StaleParameter);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain("'Id'", StringComparison.Ordinal);
        AnalyzerTestHarness.GetReportedLine(diagnostic).ShouldContain("var id = Id;", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_parameter_read_in_initialization_of_a_component_with_two_routes_is_reported()
    {
        // Arrange
        const string routes = "[Route(\"/items/new\")]\n[Route(\"/items/{Id}/edit\")]";
        var source = StaleParameterSources.Page(routes, "[Parameter] public string? Id { get; set; }", StaleParameterSources.OnInitialized("Name = Id;"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.StaleParameter);
    }

    [Fact]
    public async Task A_parameter_read_in_a_lambda_in_initialization_is_reported()
    {
        // Arrange
        var source = StaleParameterSources.Page(IdRoute, IdParameter, StaleParameterSources.OnInitialized("Func<Guid?> read = () => Id;"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.StaleParameter);
    }

    [Fact]
    public async Task A_query_parameter_read_in_initialization_is_reported()
    {
        // Arrange
        const string members = "[Parameter, SupplyParameterFromQuery] public string? Filter { get; set; }";
        var source = StaleParameterSources.Page("[Route(\"/items\")]", members, StaleParameterSources.OnInitialized("Name = Filter;"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(BlazorDiagnosticIds.StaleParameter);
    }

    [Fact]
    public async Task A_parameter_read_in_parameters_set_is_not_reported()
    {
        // Arrange
        var source = StaleParameterSources.Page(IdRoute, IdParameter, StaleParameterSources.OnParametersSetAsync("var id = Id;"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_parameter_read_in_a_component_without_a_route_is_not_reported()
    {
        // Arrange
        var source = StaleParameterSources.Page(string.Empty, IdParameter, StaleParameterSources.OnInitializedAsync("var id = Id;"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_parameter_read_in_a_component_with_one_parameterless_route_is_not_reported()
    {
        // Arrange
        var source = StaleParameterSources.Page("[Route(\"/items\")]", IdParameter, StaleParameterSources.OnInitializedAsync("var id = Id;"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_read_of_a_property_that_is_not_a_parameter_is_not_reported()
    {
        // Arrange
        var source = StaleParameterSources.Page(IdRoute, IdParameter, StaleParameterSources.OnInitialized("var name = Name;"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_nameof_use_of_a_parameter_is_not_reported()
    {
        // Arrange
        var source = StaleParameterSources.Page(IdRoute, IdParameter, StaleParameterSources.OnInitialized("Name = nameof(Id);"));

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_class_that_is_not_a_component_is_not_reported()
    {
        // Arrange
        const string source = """
            namespace Demo;

            [Route("/items/{Id}")]
            public sealed class Plain
            {
                [Parameter] public string? Id { get; set; }

                public void OnInitialized()
                {
                    var id = Id;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
