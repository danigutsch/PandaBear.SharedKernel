namespace SharedKernel.Documentation.Tests;

public sealed class EndpointRoutePolicyTests
{
    [Fact]
    public void Default_policy_does_not_infer_application_audiences_or_metadata()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("src/Api/Endpoints.cs", """
            app.MapGet("/api/v1/public/items", GetItems)
                .WithAdminMetadata("ApplicationName");
            """);
        var source = Path.Combine(workspace.RootPath, "src");

        // Act
        var table = EndpointRouteMarkdownTable.Build(source, new DocumentationGeneratorBlock());

        // Assert
        table.ShouldContain("| unspecified |", StringComparison.Ordinal);
        table.ShouldNotContain("ApplicationName", StringComparison.Ordinal);
    }

    [Fact]
    public void Consumer_configuration_supplies_endpoint_metadata_and_audiences()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("src/Api/Endpoints.cs", """
            app.MapGet("/external/items", GetItems)
                .WithEndpointMetadata("ItemList");
            """);
        var configuration = new DocumentationGeneratorBlock
        {
            EndpointNameMethods = ["WithEndpointMetadata", "WithName"],
            RouteAudiences = { ["^/external/"] = "external clients" },
            DefaultAudience = "internal",
        };

        // Act
        var table = EndpointRouteMarkdownTable.Build(Path.Combine(workspace.RootPath, "src"), configuration);

        // Assert
        table.ShouldContain("| ItemList | external clients |", StringComparison.Ordinal);
    }
}
