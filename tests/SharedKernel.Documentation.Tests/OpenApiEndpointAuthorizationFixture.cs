namespace PandaBear.Documentation.Tests;

/// <summary>Creates a deterministic source inventory and matching generated OpenAPI input.</summary>
internal static class OpenApiEndpointAuthorizationFixture
{
    /// <summary>The relative generator configuration path.</summary>
    public const string ConfigurationPath = "docs/architecture/generated-diagrams.json";
    /// <summary>The configured Admin OpenAPI artifact path.</summary>
    public const string SnapshotPath = "artifacts/admin.openapi.json";
    /// <summary>The full maintenance timeline pattern.</summary>
    public const string TimelinePath = "/api/v1/operations/document-maintenance/{job}";
    /// <summary>The source mapping that inherits authorization through a route group.</summary>
    public const string Source = """
        var operations = app.MapOperationsGroup();
        operations.MapGet("/document-maintenance/{job}", GetTimeline)
            .WithAdminMetadata("GetDocumentMaintenanceTimeline", "Reads one job's history.");
        """;
    /// <summary>The stock OpenAPI operation identity and explicit Bearer security.</summary>
    public const string SecureOperation = """
        "operationId": "GetDocumentMaintenanceTimeline", "security": [{ "Bearer": [] }]
        """;

    private const string BearerComponents = """ "components": { "securitySchemes": { "Bearer": { "type": "http", "scheme": "bearer" } } }, """;

    /// <summary>Generates the table in the test workspace.</summary>
    /// <param name="workspace">The configured isolated documentation workspace.</param>
    /// <returns>The updated Markdown document.</returns>
    public static string Generate(TemporaryDocumentationWorkspace workspace)
    {
        DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: false);
        return workspace.ReadArchitectureDoc("overview.md");
    }

    /// <summary>Writes one API scope and synthetic OpenAPI document to an isolated workspace.</summary>
    /// <param name="operation">The operation identity and optional security fields as JSON.</param>
    /// <param name="optIn">Whether to configure the generated OpenAPI artifact.</param>
    /// <param name="path">The OpenAPI path key.</param>
    /// <param name="method">The OpenAPI method key.</param>
    /// <param name="extraPath">Any additional OpenAPI path entry.</param>
    /// <param name="additionalDocuments">Additional configured API source scopes.</param>
    /// <param name="components">The generated document components property as JSON.</param>
    /// <returns>The configured temporary workspace.</returns>
    public static TemporaryDocumentationWorkspace CreateWorkspace(
        string operation, bool optIn = true, string path = TimelinePath, string method = "get",
        string extraPath = "", string additionalDocuments = "", string components = BearerComponents)
    {
        var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("src/Admin.Api/Endpoints.cs", Source);
        workspace.WriteFile(SnapshotPath, $$"""
            {
              "openapi": "3.2.0",
              {{components}}
              "paths": {
                "{{path}}": {
                  "{{method}}": {
                    {{operation}},
                    "responses": { "401": { "description": "Missing token" }, "403": { "description": "Denied" } }
                  }
                }{{extraPath}}
              }
            }
            """);
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("Before"));
        var evidence = optIn ? $$"""
            ,
            "openApiAuthorizationDocuments": { "Admin.Api/": "artifacts/admin.openapi.json"{{additionalDocuments}} }
            """ : string.Empty;
        workspace.WriteConfig($$"""
            {
              "docsPath": "docs/architecture",
              "generatorName": "test-generator",
              "blocks": [{
                "name": "sample",
                "kind": "endpoint-routes",
                "sourcePath": "src",
                "targetPath": "overview.md",
                "routePrefixes": { "operations": "/api/v1/operations" },
                "endpointNameMethods": ["WithAdminMetadata", "WithName"]{{evidence}}
              }]
            }
            """);
        return workspace;
    }
}
