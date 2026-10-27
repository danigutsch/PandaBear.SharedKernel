namespace PandaBear.Documentation.Tests;

/// <summary>Tests that the documentation conformance checker rejects configurations and documents whose governed content, fact lists or source facts are missing, unbalanced or inconsistent.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentationGenerationCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class DocumentationConformanceCheckerTests
{
    [Fact]
    public void Check_rejects_a_removed_documentation_index_table_while_fact_identifiers_remain()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.ExpectedFactConformanceConfig());
        workspace.WriteFile(
            "docs/README.md",
            "<!-- doc-content:documentation-index-table:start -->\n<!-- doc-content:documentation-index-table:end -->\n\n<!-- doc-fact:documentation-index:start -->\n- current-requirement: `deprecated-docs-reviewed`\n- current-requirement: `guidance-centralized`\n<!-- doc-fact:documentation-index:end -->");
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("documentation-index-table", StringComparison.Ordinal);
        exception.Message.ShouldContain("docs/README.md", StringComparison.Ordinal);
        exception.Message.ShouldContain("meaningful", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("required-sections-checklist")]
    [InlineData("provenance-expectations")]
    [InlineData("small-doc-exemption")]
    public void Check_rejects_removed_governance_content_while_fact_identifiers_remain(string removedBlock)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.GovernanceFactConformanceConfig());
        workspace.WriteFile(
            "docs/DOCUMENTATION_GOVERNANCE.md",
            DocumentationTestContent.GovernanceDocumentWithoutContent(removedBlock));
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain(removedBlock, StringComparison.Ordinal);
        exception.Message.ShouldContain("docs/DOCUMENTATION_GOVERNANCE.md", StringComparison.Ordinal);
        exception.Message.ShouldContain("meaningful", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<!-- doc-content:documentation-index-table:start -->\n| Topic | Source |")]
    [InlineData("<!-- doc-content:documentation-index-table:end -->\n| Topic | Source |\n<!-- doc-content:documentation-index-table:start -->")]
    [InlineData("<!-- doc-content:documentation-index-table:start -->\n| Topic | Source |\n<!-- doc-content:documentation-index-table:start -->\n<!-- doc-content:documentation-index-table:end -->")]
    [InlineData("<!-- doc-content:documentation-index-table:start -->\n<!-- retained marker only -->\n<!-- doc-content:documentation-index-table:end -->")]
    public void Check_rejects_unbalanced_or_marker_only_governed_content(string contentBlock)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.ExpectedFactConformanceConfig());
        workspace.WriteFile(
            "docs/README.md",
            $"{contentBlock}\n\n<!-- doc-fact:documentation-index:start -->\n- current-requirement: `deprecated-docs-reviewed`\n- current-requirement: `guidance-centralized`\n<!-- doc-fact:documentation-index:end -->");
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("documentation-index-table", StringComparison.Ordinal);
        exception.Message.ShouldContain("docs/README.md", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_rejects_null_content_block_marker_collections()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(
            "docs/architecture/documentation-conformance.json",
            """
            {
              "checks": [
                {
                  "name": "documentation-index",
                  "documentPath": "docs/README.md",
                  "markerName": "documentation-index",
                  "factName": "current-requirement",
                  "expectedIdentifiers": ["guidance-centralized"],
                  "contentBlockMarkers": null
                }
              ]
            }
            """);
        workspace.WriteFile(
            "docs/README.md",
            "<!-- doc-fact:documentation-index:start -->\n- current-requirement: `guidance-centralized`\n<!-- doc-fact:documentation-index:end -->");
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("documentation-conformance.json", StringComparison.Ordinal);
        exception.Message.ShouldContain("null fact check collections", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_rejects_empty_check_collections()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", "{ \"checks\": [] }");
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("documentation-conformance.json", StringComparison.Ordinal);
        exception.Message.ShouldContain("checks", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_rejects_switch_sources_without_dispatch_facts()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-events:start -->\n- current-event: `FirstEvent`\n<!-- doc-fact:sample-events:end -->");
        workspace.WriteFile(
            "src/SampleAggregate.cs",
            "internal sealed class SampleAggregate { private static int Apply(object value) => value is null ? 0 : 1; }");
        workspace.WriteFile(
            "src/SampleProjection.cs",
            "internal sealed class SampleProjection { private static int Apply(object value) => value is null ? 0 : 1; }");
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("src/SampleAggregate.cs", StringComparison.Ordinal);
        exception.Message.ShouldContain("no switch facts", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_rejects_registration_sources_without_matching_facts()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.RegistrationFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-runtime:start -->\n- current-runtime: `AddIntegrationEventOutbox`\n<!-- doc-fact:sample-runtime:end -->");
        workspace.WriteFile(
            "src/SampleRuntime.cs",
            """
            internal static class SampleRuntime
            {
                public static object Entry(object builder) => builder.Configure(addRuntime: null);

                private static void Configure(object services) =>
                    services.AddHostedService<DocumentAuditRetentionHostedService>();
            }
            """);
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("docs/architecture/FLOWS.md", StringComparison.Ordinal);
        exception.Message.ShouldContain("no registration facts", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_rejects_top_level_argument_requirements()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(
            "docs/architecture/documentation-conformance.json",
            """
            {
              "checks": [
                {
                  "name": "top-level",
                  "documentPath": "docs/FLOWS.md",
                  "markerName": "top-level",
                  "factName": "current-fact",
                  "expectedIdentifiers": ["stable"],
                  "invocationRequirements": [
                    {
                      "sourcePath": "src/Program.cs",
                      "invokedMethodName": "Configure",
                      "expectedCount": 1,
                      "expectedArguments": ["addRuntime: null"]
                    }
                  ]
                }
              ]
            }
            """);
        workspace.WriteFile(
            "docs/FLOWS.md",
            "<!-- doc-fact:top-level:start -->\n- current-fact: `stable`\n<!-- doc-fact:top-level:end -->");
        workspace.WriteFile("src/Program.cs", "builder.Configure(addRuntime: null);");
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("src/Program.cs", StringComparison.Ordinal);
        exception.Message.ShouldContain("top-level invocation arguments", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_rejects_unexpected_invocation_arguments()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.RegistrationFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-runtime:start -->\n- current-runtime: `AddIntegrationEventOutbox`\n- current-runtime: `CatalogProjectionHostedService`\n<!-- doc-fact:sample-runtime:end -->");
        workspace.WriteFile(
            "src/SampleRuntime.cs",
            DocumentationTestContent.SampleRuntimeSource().Replace("addRuntime: null", "addRuntime: false", StringComparison.Ordinal));
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("src/SampleRuntime.cs", StringComparison.Ordinal);
        exception.Message.ShouldContain("unexpected arguments", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_rejects_registration_checks_without_identifier_filters()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(
            "docs/architecture/documentation-conformance.json",
            """
            {
              "checks": [
                {
                  "name": "runtime",
                  "documentPath": "docs/FLOWS.md",
                  "markerName": "runtime",
                  "factName": "current-runtime",
                  "registrationSources": [
                    {
                      "sourcePath": "src/Runtime.cs",
                      "methodName": "Configure",
                      "parameterCount": 1
                    }
                  ]
                }
              ]
            }
            """);
        Action act = () => DocumentationConformanceChecker.Check(
            workspace.RootPath,
            "docs/architecture/documentation-conformance.json");

        // Act
        var exception = act.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("runtime", StringComparison.Ordinal);
        exception.Message.ShouldContain("identifier filters", StringComparison.Ordinal);
    }
}
