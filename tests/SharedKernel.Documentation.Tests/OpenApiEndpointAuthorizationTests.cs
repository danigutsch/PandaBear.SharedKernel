using Fixture = SharedKernel.Documentation.Tests.OpenApiEndpointAuthorizationFixture;

namespace SharedKernel.Documentation.Tests;

/// <summary>Verifies fixed-host OpenAPI authorization evidence in generated endpoint tables.</summary>
[Trait(Testing.TestTraitNames.CapabilityName, TestTraits.DocumentationGenerationCapability)]
[Trait(Testing.TestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class OpenApiEndpointAuthorizationTests
{
    [Fact]
    public void Named_inherited_authorization_uses_the_matching_API_scope()
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation);
        workspace.WriteFile("src/Admin.ApiReplica/OtherHost.cs", Fixture.Source);

        // Act
        var table = Fixture.Generate(workspace);

        // Assert
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | required | `Admin.Api/Endpoints.cs` |", StringComparison.Ordinal);
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | not declared | `Admin.ApiReplica/OtherHost.cs` |", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""
        "operationId": "GetDocumentMaintenanceTimeline"
        """)]
    [InlineData("""
        "operationId": "GetDocumentMaintenanceTimeline", "security": []
        """)]
    [InlineData("""
        "operationId": "GetDocumentMaintenanceTimeline", "security": [{ "Bearer": [] }, {}]
        """)]
    [InlineData("""
        "operationId": "GetDocumentMaintenanceTimeline", "security": [{ "Bearer": [], "Undeclared": [] }]
        """)]
    [InlineData("""
        "operationId": "GetDocumentMaintenanceTimeline", "security": [{ "Bearer": false }]
        """)]
    [InlineData("""
        "operationId": "GetDocumentMaintenanceTimeline", "security": [null]
        """)]
    [InlineData("""
        "operationId": "GetDocumentMaintenanceTimeline", "security": [{ "Bearer": [false] }]
        """)]
    [InlineData("""
        "security": [{ "Bearer": [] }]
        """)]
    [InlineData("""
        "operationId": "", "security": [{ "Bearer": [] }]
        """)]
    [InlineData("""
        "operationId": 12, "security": [{ "Bearer": [] }]
        """)]
    public void Missing_optional_or_undeclared_security_keeps_the_source_classification(string operation)
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(operation);

        // Act
        var table = Fixture.Generate(workspace);

        // Assert
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | not declared |", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/v1/operations/document-maintenance/{other}", "get")]
    [InlineData(Fixture.TimelinePath, "post")]
    [InlineData(Fixture.TimelinePath, "x-observation")]
    public void A_name_match_alone_does_not_mark_a_different_operation_required(string snapshotPath, string snapshotMethod)
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation, path: snapshotPath, method: snapshotMethod);

        // Act
        var table = Fixture.Generate(workspace);

        // Assert
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | not declared |", StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_operation_IDs_leave_the_source_classification()
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation, extraPath: """
            ,
            "/api/v1/operations/other": {
              "get": { "operationId": "GetDocumentMaintenanceTimeline" }
            }
            """);

        // Act
        var table = Fixture.Generate(workspace);

        // Assert
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | not declared |", StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_source_anonymity_takes_precedence_over_positive_OpenApi_evidence()
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation);
        workspace.WriteFile("src/Admin.Api/Endpoints.cs", Fixture.Source.TrimEnd(';') + ".AllowAnonymous();");

        // Act
        var table = Fixture.Generate(workspace);

        // Assert
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | anonymous |", StringComparison.Ordinal);
    }

    [Fact]
    public void An_implicit_handler_name_does_not_attach_OpenApi_evidence()
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation);
        workspace.WriteFile("src/Admin.Api/Endpoints.cs", """
            var operations = app.MapOperationsGroup();
            operations.MapGet("/document-maintenance/{job}", GetDocumentMaintenanceTimeline);
            """);

        // Act
        var table = Fixture.Generate(workspace);

        // Assert
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | not declared |", StringComparison.Ordinal);
    }

    [Fact]
    public void Overlapping_source_scopes_leave_the_source_classification()
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation, additionalDocuments: """
            , "Admin.Api/Sub/": "artifacts/admin.openapi.json"
            """);
        workspace.WriteFile("src/Admin.Api/Sub/Endpoints.cs", Fixture.Source);

        // Act
        var table = Fixture.Generate(workspace);

        // Assert
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | required | `Admin.Api/Endpoints.cs` |", StringComparison.Ordinal);
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | not declared | `Admin.Api/Sub/Endpoints.cs` |", StringComparison.Ordinal);
    }

    [Fact]
    public void Default_configuration_keeps_the_source_only_inventory()
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation, optIn: false);

        // Act
        var table = Fixture.Generate(workspace);

        // Assert
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | not declared |", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(""" "components": null, """)]
    [InlineData(""" "components": {}, """)]
    [InlineData(""" "components": { "securitySchemes": [] }, """)]
    [InlineData(""" "components": { "securitySchemes": { "Bearer": [] } }, """)]
    [InlineData(""" "components": { "securitySchemes": { "Bearer": { "$ref": "#/components/securitySchemes/Other" } } }, """)]
    [InlineData(""" "components": { "securitySchemes": { "Bearer": { "type": false } } }, """)]
    [InlineData(""" "components": { "securitySchemes": { "Bearer": { "type": "unknown" } } }, """)]
    public void Unavailable_or_invalid_schemes_do_not_supply_protection(string components)
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation, components: components);

        // Act
        var table = Fixture.Generate(workspace);

        // Assert
        table.ShouldContain("| GetDocumentMaintenanceTimeline | unspecified | not declared |", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(""" , "": "artifacts/admin.openapi.json" """)]
    [InlineData(""" , "Other.Api/": "" """)]
    public void Configured_evidence_requires_a_scope_and_artifact_path(string additionalDocuments)
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation, additionalDocuments: additionalDocuments);
        Action verify = () => DocumentationGenerator.Run(workspace.RootPath, Fixture.ConfigurationPath, checkOnly: true);

        // Act
        var failure = verify.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("requires a source prefix and a document path", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "paths": "invalid" }""")]
    [InlineData("""{ "openapi": "3.2.0", "paths": "invalid" }""")]
    [InlineData("""{ "openapi": "3.2.0", "paths": {}, "paths": {} }""")]
    public void Invalid_configured_OpenApi_reports_the_artifact_path(string invalidDocument)
    {
        // Arrange
        using var workspace = Fixture.CreateWorkspace(Fixture.SecureOperation);
        workspace.WriteFile(Fixture.SnapshotPath, invalidDocument);
        Action verify = () => DocumentationGenerator.Run(workspace.RootPath, Fixture.ConfigurationPath, checkOnly: true);

        // Act
        var failure = verify.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(Fixture.SnapshotPath, StringComparison.Ordinal);
    }
}
