using System.Globalization;

namespace PandaBear.Documentation.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.CommandLineCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class DocumentationToolApplicationTests
{
    [Fact]
    public async Task Run_returns_error_when_config_is_missing()
    {
        // Arrange
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(["generate"], output, error, Directory.GetCurrentDirectory());
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Missing required --config <path>.", StringComparison.Ordinal);
        errorText.ShouldContain("Usage: sharedkernel-docs generate --config <path> [--check]", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_returns_error_when_config_file_is_missing()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["generate", "--config", "docs/architecture/generated-diagrams.json"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Documentation generation failed:", StringComparison.Ordinal);
        errorText.ShouldContain("generated-diagrams.json", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_reports_the_block_and_target_when_generated_markers_are_missing()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", "# Overview");
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["generate", "--config", "docs/architecture/generated-diagrams.json", "--check"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("sample", StringComparison.Ordinal);
        errorText.ShouldContain("docs/architecture/overview.md", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_returns_error_when_config_value_is_missing()
    {
        // Arrange
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(["generate", "--config"], output, error, Directory.GetCurrentDirectory());
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Missing required value for --config.", StringComparison.Ordinal);
        errorText.ShouldContain("Usage: sharedkernel-docs generate --config <path> [--check]", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_returns_error_when_config_value_is_another_option()
    {
        // Arrange
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(["generate", "--config", "--check"], output, error, Directory.GetCurrentDirectory());
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Missing required value for --config.", StringComparison.Ordinal);
        errorText.ShouldContain("Usage: sharedkernel-docs generate --config <path> [--check]", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_returns_error_when_command_is_unknown()
    {
        // Arrange
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(["unknown"], output, error, Directory.GetCurrentDirectory());
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Unknown command: unknown", StringComparison.Ordinal);
        errorText.ShouldContain("Usage: sharedkernel-docs generate --config <path> [--check]", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_returns_error_when_argument_is_unknown()
    {
        // Arrange
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(["generate", "--unknown"], output, error, Directory.GetCurrentDirectory());
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Unknown argument: --unknown", StringComparison.Ordinal);
        errorText.ShouldContain("Usage: sharedkernel-docs generate --config <path> [--check]", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_returns_error_when_config_is_missing()
    {
        // Arrange
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(["check"], output, error, Directory.GetCurrentDirectory());
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Missing required --config <path>.", StringComparison.Ordinal);
        errorText.ShouldContain("sharedkernel-docs check --config <path>", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task Check_returns_exact_error_when_config_value_is_missing(string newLine)
    {
        // Arrange
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture) { NewLine = newLine };
        var expectedError = string.Join(
            newLine,
            "Missing required value for --config.",
            "Usage: sharedkernel-docs generate --config <path> [--check]",
            "       sharedkernel-docs check --config <path>",
            string.Empty);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config"],
            output,
            error,
            Directory.GetCurrentDirectory());

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe(expectedError);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task Check_reports_the_first_extra_argument_exactly(string newLine)
    {
        // Arrange
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture) { NewLine = newLine };
        var expectedError = string.Join(
            newLine,
            "Unknown argument: --unknown",
            "Usage: sharedkernel-docs generate --config <path> [--check]",
            "       sharedkernel-docs check --config <path>",
            string.Empty);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json", "--unknown"],
            output,
            error,
            Directory.GetCurrentDirectory());

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe(expectedError);
    }

    [Fact]
    public async Task Check_returns_error_when_argument_is_unknown()
    {
        // Arrange
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--unknown"],
            output,
            error,
            Directory.GetCurrentDirectory());
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Unknown argument: --unknown", StringComparison.Ordinal);
        errorText.ShouldContain("sharedkernel-docs check --config <path>", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_reports_current_when_generated_documentation_is_current()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));
        DocumentationGenerator.Run(workspace.RootPath, "docs/architecture/generated-diagrams.json", checkOnly: false);
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["generate", "--config", "docs/architecture/generated-diagrams.json", "--check"],
            output,
            error,
            workspace.RootPath);
        var outputText = output.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("Generated documentation is current.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_check_reports_stale_generated_documentation()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["generate", "--config", "docs/architecture/generated-diagrams.json", "--check"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Generated documentation is stale:", StringComparison.Ordinal);
        errorText.ShouldContain("docs/architecture/overview.md", StringComparison.Ordinal);
        errorText.ShouldContain("Run without --check to refresh generated documentation.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_updates_generated_documentation()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["generate", "--config", "docs/architecture/generated-diagrams.json"],
            output,
            error,
            workspace.RootPath);
        var outputText = output.ToString();
        var updated = workspace.ReadArchitectureDoc("overview.md");

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("Updated generated documentation:", StringComparison.Ordinal);
        outputText.ShouldContain("docs/architecture/overview.md", StringComparison.Ordinal);
        updated.ShouldContain("newNode[New node]", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_accepts_matching_switch_facts_and_ignores_unrelated_is_patterns()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-events:start -->\n- current-event: `FirstEvent`\n- current-event: `SecondEvent`\n<!-- doc-fact:sample-events:end -->");
        workspace.WriteFile("src/SampleAggregate.cs", DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile("src/SampleProjection.cs", DocumentationTestContent.SampleProjectionSource());
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);
        var outputText = output.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("Documentation conformance checks passed.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_rejects_unrelated_is_patterns_documented_as_events()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-events:start -->\n- current-event: `bool`\n- current-event: `FirstEvent`\n- current-event: `SecondEvent`\n<!-- doc-fact:sample-events:end -->");
        workspace.WriteFile("src/SampleAggregate.cs", DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile("src/SampleProjection.cs", DocumentationTestContent.SampleProjectionSource());
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("docs/architecture/FLOWS.md", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<!-- doc-fact:sample-events:start -->\n- current-event: `Missing end\n<!-- doc-fact:sample-events:end -->")]
    [InlineData("<!-- doc-fact:sample-events:start -->\n<!-- doc-fact:sample-events:end -->")]
    [InlineData("<!-- doc-fact:sample-events:start -->\n- current-event: `FirstEvent`\n- current-event: `FirstEvent`\n<!-- doc-fact:sample-events:end -->")]
    public async Task Check_reports_the_exact_document_path_for_malformed_or_duplicate_facts(string content)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile("docs/architecture/FLOWS.md", content);
        workspace.WriteFile("src/SampleAggregate.cs", DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile("src/SampleProjection.cs", DocumentationTestContent.SampleProjectionSource());
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("docs/architecture/FLOWS.md", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_filters_unrelated_hosted_services_from_runtime_facts()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.RegistrationFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-runtime:start -->\n- current-runtime: `AddIntegrationEventOutbox`\n- current-runtime: `CatalogProjectionHostedService`\n<!-- doc-fact:sample-runtime:end -->");
        workspace.WriteFile("src/SampleRuntime.cs", DocumentationTestContent.SampleRuntimeSource());
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Check_reports_missing_required_runtime_invocations()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.RegistrationFactConformanceConfig(requireMissingInvocation: true));
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-runtime:start -->\n- current-runtime: `AddIntegrationEventOutbox`\n- current-runtime: `CatalogProjectionHostedService`\n<!-- doc-fact:sample-runtime:end -->");
        workspace.WriteFile("src/SampleRuntime.cs", DocumentationTestContent.SampleRuntimeSource());
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("src/SampleRuntime.cs", StringComparison.Ordinal);
        errorText.ShouldContain("Missing", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_accepts_configured_machine_readable_facts()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.ExpectedFactConformanceConfig());
        workspace.WriteFile(
            "docs/README.md",
            "<!-- doc-content:documentation-index-table:start -->\n| Topic | Canonical source |\n| --- | --- |\n| Guidance | `docs/GUIDANCE.md` |\n<!-- doc-content:documentation-index-table:end -->\n\n<!-- doc-fact:documentation-index:start -->\n- current-requirement: `deprecated-docs-reviewed`\n- current-requirement: `guidance-centralized`\n<!-- doc-fact:documentation-index:end -->");
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Check_rejects_source_symlinks_outside_the_repository()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        using var outsideSource = new TemporaryTextDocument(DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-events:start -->\n- current-event: `FirstEvent`\n- current-event: `SecondEvent`\n<!-- doc-fact:sample-events:end -->");
        workspace.WriteFile("src/SampleProjection.cs", DocumentationTestContent.SampleProjectionSource());
        File.CreateSymbolicLink(Path.Combine(workspace.RootPath, "src", "SampleAggregate.cs"), outsideSource.Path);
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("symbolic link", StringComparison.OrdinalIgnoreCase);
        errorText.ShouldContain("src/SampleAggregate.cs", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_reports_null_fact_configuration_collections()
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
                  "expectedIdentifiers": null
                }
              ]
            }
            """);
        workspace.WriteFile(
            "docs/README.md",
            "<!-- doc-fact:documentation-index:start -->\n- current-requirement: `guidance-centralized`\n<!-- doc-fact:documentation-index:end -->");
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("documentation-conformance.json", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_reports_mismatched_switch_sources_with_the_document_and_source_paths()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile("docs/architecture/documentation-conformance.json", DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-events:start -->\n- current-event: `FirstEvent`\n- current-event: `SecondEvent`\n<!-- doc-fact:sample-events:end -->");
        workspace.WriteFile("src/SampleAggregate.cs", DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile(
            "src/SampleProjection.cs",
            """
            internal sealed class SampleProjection
            {
                private static int Apply(object value) => value switch
                {
                    FirstEvent { } => 1,
                    ThirdEvent { } => 3,
                    _ => 0
                };
            }
            """);
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("docs/architecture/FLOWS.md", StringComparison.Ordinal);
        errorText.ShouldContain("src/SampleProjection.cs", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_reports_null_check_entries()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(
            "docs/architecture/documentation-conformance.json",
            """
            {
              "checks": [null]
            }
            """);
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        await using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await DocumentationToolApplication.Run(
            ["check", "--config", "docs/architecture/documentation-conformance.json"],
            output,
            error,
            workspace.RootPath);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("documentation-conformance.json", StringComparison.Ordinal);
    }
}
