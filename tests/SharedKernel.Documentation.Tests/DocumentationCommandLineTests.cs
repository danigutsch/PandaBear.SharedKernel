namespace PandaBear.Documentation.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.CommandLineCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class DocumentationCommandLineTests
{
    private const string GeneratorConfigPath = "docs/architecture/generated-diagrams.json";
    private const string ConformanceConfigPath = "docs/architecture/documentation-conformance.json";

    [Fact]
    public void Root_command_exposes_generate_verify_and_check()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();

        // Act
        var commandNames = DocumentationCommandLine.Create(workspace.RootPath)
            .Subcommands
            .Select(static command => command.Name)
            .ToArray();

        // Assert
        commandNames.ShouldBe(["generate", "verify", "check"]);
    }

    [Fact]
    public void Help_writes_the_commands_to_standard_output()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["--help"]);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("generate", StringComparison.Ordinal);
        run.Output.ShouldContain("verify", StringComparison.Ordinal);
        run.Output.ShouldContain("check", StringComparison.Ordinal);
        run.Error.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("verify")]
    [InlineData("check")]
    public void Command_help_shows_the_config_option(string command)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, [command, "--help"]);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("--config <path>", StringComparison.Ordinal);
        run.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public void Version_writes_to_standard_output()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["--version"]);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldNotBeEmpty();
        run.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public void Missing_command_reports_a_parse_error()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        var rootCommand = DocumentationCommandLine.Create(workspace.RootPath);

        // Act
        var parseResult = rootCommand.Parse([]);
        var run = DocumentationCommandLineRun.Invoke(parseResult);

        // Assert
        var parseError = parseResult.Errors.ShouldHaveSingleItem();
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain(parseError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_command_reports_a_parse_error()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["publish"]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("publish", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("verify")]
    [InlineData("check")]
    public void Commands_require_the_config_option(string command)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        var rootCommand = DocumentationCommandLine.Create(workspace.RootPath);

        // Act
        var parseResult = rootCommand.Parse([command]);
        var run = DocumentationCommandLineRun.Invoke(parseResult);

        // Assert
        parseResult.CommandResult.Command.Name.ShouldBe(command);
        var parseError = parseResult.Errors.ShouldHaveSingleItem();
        parseError.Message.ShouldContain("--config", StringComparison.Ordinal);
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain(parseError.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("verify")]
    [InlineData("check")]
    public void Commands_require_a_config_value(string command)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        var rootCommand = DocumentationCommandLine.Create(workspace.RootPath);

        // Act
        var parseResult = rootCommand.Parse([command, "--config"]);
        var run = DocumentationCommandLineRun.Invoke(parseResult);

        // Assert
        parseResult.CommandResult.Command.Name.ShouldBe(command);
        var parseError = parseResult.Errors.ShouldHaveSingleItem();
        parseError.Message.ShouldContain("--config", StringComparison.Ordinal);
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain(parseError.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--config", 0)]
    [InlineData("--confg", 1)]
    public void Diagram_directive_reports_whether_generate_parses_without_writing_files(string option, int expectedExitCode)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        var staleDocument = DocumentationTestContent.GeneratedBlockDocument("old[Old]");
        workspace.WriteArchitectureDoc("overview.md", staleDocument);
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["[diagram]", "generate", option, GeneratorConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(expectedExitCode, "Consumer caller tests read the parse result from the exit code.");
        run.Output.ShouldContain("[ generate", StringComparison.Ordinal);
        workspace.ReadArchitectureDoc("overview.md").ShouldBe(staleDocument, "The directive runs instead of the command, so generate writes nothing.");
    }

    [Fact]
    public void Generate_rejects_unrecognized_options_without_writing_files()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        var staleDocument = DocumentationTestContent.GeneratedBlockDocument("old[Old]");
        workspace.WriteArchitectureDoc("overview.md", staleDocument);
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));
        var rootCommand = DocumentationCommandLine.Create(workspace.RootPath);

        // Act
        var parseResult = rootCommand.Parse(["generate", "--config", GeneratorConfigPath, "--check"]);
        var run = DocumentationCommandLineRun.Invoke(parseResult);

        // Assert
        parseResult.CommandResult.Command.Name.ShouldBe("generate");
        var parseError = parseResult.Errors.ShouldHaveSingleItem();
        parseError.Message.ShouldContain("--check", StringComparison.Ordinal);
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain(parseError.Message, StringComparison.Ordinal);
        workspace.ReadArchitectureDoc("overview.md").ShouldBe(staleDocument);
    }

    [Fact]
    public void Check_rejects_unrecognized_arguments_without_running_checks()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(ConformanceConfigPath, DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-events:start -->\n- current-event: `FirstEvent`\n- current-event: `SecondEvent`\n<!-- doc-fact:sample-events:end -->");
        workspace.WriteFile("src/SampleAggregate.cs", DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile("src/SampleProjection.cs", DocumentationTestContent.SampleProjectionSource());
        var rootCommand = DocumentationCommandLine.Create(workspace.RootPath);

        // Act
        var parseResult = rootCommand.Parse(["check", "--config", ConformanceConfigPath, "--unknown"]);
        var run = DocumentationCommandLineRun.Invoke(parseResult);

        // Assert
        parseResult.CommandResult.Command.Name.ShouldBe("check");
        var parseError = parseResult.Errors.ShouldHaveSingleItem();
        parseError.Message.ShouldContain("--unknown", StringComparison.Ordinal);
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain(parseError.Message, StringComparison.Ordinal);
        run.Output.ShouldNotContain("Documentation conformance checks passed.", StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_updates_stale_generated_documentation()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["generate", "--config", GeneratorConfigPath]);
        var updated = workspace.ReadArchitectureDoc("overview.md");

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Updated generated documentation:", StringComparison.Ordinal);
        run.Output.ShouldContain("- docs/architecture/overview.md", StringComparison.Ordinal);
        run.Error.ShouldBe(string.Empty);
        updated.ShouldContain("newNode[New node]", StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_reports_current_generated_documentation()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));
        DocumentationGenerator.Run(workspace.RootPath, GeneratorConfigPath, checkOnly: false);

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["generate", "--config", GeneratorConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Generated documentation is current.", StringComparison.Ordinal);
        run.Error.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("verify")]
    public void Generation_commands_report_a_missing_config_file(string command)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, [command, "--config", GeneratorConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("Documentation generation failed:", StringComparison.Ordinal);
        run.Error.ShouldContain("generated-diagrams.json", StringComparison.Ordinal);
        run.Output.ShouldBe(string.Empty);
    }

    [Fact]
    public void Verify_reports_current_generated_documentation()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));
        DocumentationGenerator.Run(workspace.RootPath, GeneratorConfigPath, checkOnly: false);

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["verify", "--config", GeneratorConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Generated documentation is current.", StringComparison.Ordinal);
        run.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public void Verify_reports_stale_generated_documentation_on_standard_error_without_writing_files()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        var staleDocument = DocumentationTestContent.GeneratedBlockDocument("old[Old]");
        workspace.WriteArchitectureDoc("overview.md", staleDocument);
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));
        var expectedError = string.Join(
            Environment.NewLine,
            "Generated documentation is stale:",
            "- docs/architecture/overview.md",
            $"Run 'generate --config {GeneratorConfigPath}' to refresh generated documentation.",
            string.Empty);

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["verify", "--config", GeneratorConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldBe(expectedError);
        run.Output.ShouldBe(string.Empty);
        workspace.ReadArchitectureDoc("overview.md").ShouldBe(staleDocument);
    }

    [Fact]
    public void Verify_reports_the_block_and_target_when_generated_markers_are_missing()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", "# Overview");
        workspace.WriteConfig(DocumentationTestContent.GeneratorConfig("newNode[New node]"));

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["verify", "--config", GeneratorConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("sample", StringComparison.Ordinal);
        run.Error.ShouldContain("docs/architecture/overview.md", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_accepts_matching_switch_facts_and_ignores_unrelated_is_patterns()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(ConformanceConfigPath, DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-events:start -->\n- current-event: `FirstEvent`\n- current-event: `SecondEvent`\n<!-- doc-fact:sample-events:end -->");
        workspace.WriteFile("src/SampleAggregate.cs", DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile("src/SampleProjection.cs", DocumentationTestContent.SampleProjectionSource());

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Documentation conformance checks passed.", StringComparison.Ordinal);
        run.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public void Check_rejects_unrelated_is_patterns_documented_as_events()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(ConformanceConfigPath, DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-events:start -->\n- current-event: `bool`\n- current-event: `FirstEvent`\n- current-event: `SecondEvent`\n<!-- doc-fact:sample-events:end -->");
        workspace.WriteFile("src/SampleAggregate.cs", DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile("src/SampleProjection.cs", DocumentationTestContent.SampleProjectionSource());

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("Documentation conformance failed:", StringComparison.Ordinal);
        run.Error.ShouldContain("docs/architecture/FLOWS.md", StringComparison.Ordinal);
        run.Output.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("<!-- doc-fact:sample-events:start -->\n- current-event: `Missing end\n<!-- doc-fact:sample-events:end -->")]
    [InlineData("<!-- doc-fact:sample-events:start -->\n<!-- doc-fact:sample-events:end -->")]
    [InlineData("<!-- doc-fact:sample-events:start -->\n- current-event: `FirstEvent`\n- current-event: `FirstEvent`\n<!-- doc-fact:sample-events:end -->")]
    public void Check_reports_the_exact_document_path_for_malformed_or_duplicate_facts(string content)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(ConformanceConfigPath, DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile("docs/architecture/FLOWS.md", content);
        workspace.WriteFile("src/SampleAggregate.cs", DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile("src/SampleProjection.cs", DocumentationTestContent.SampleProjectionSource());

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("docs/architecture/FLOWS.md", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_filters_unrelated_hosted_services_from_runtime_facts()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(ConformanceConfigPath, DocumentationTestContent.RegistrationFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-runtime:start -->\n- current-runtime: `AddIntegrationEventOutbox`\n- current-runtime: `CatalogProjectionHostedService`\n<!-- doc-fact:sample-runtime:end -->");
        workspace.WriteFile("src/SampleRuntime.cs", DocumentationTestContent.SampleRuntimeSource());

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Check_reports_missing_required_runtime_invocations()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(ConformanceConfigPath, DocumentationTestContent.RegistrationFactConformanceConfig(requireMissingInvocation: true));
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-runtime:start -->\n- current-runtime: `AddIntegrationEventOutbox`\n- current-runtime: `CatalogProjectionHostedService`\n<!-- doc-fact:sample-runtime:end -->");
        workspace.WriteFile("src/SampleRuntime.cs", DocumentationTestContent.SampleRuntimeSource());

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("src/SampleRuntime.cs", StringComparison.Ordinal);
        run.Error.ShouldContain("Missing", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_accepts_configured_machine_readable_facts()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(ConformanceConfigPath, DocumentationTestContent.ExpectedFactConformanceConfig());
        workspace.WriteFile(
            "docs/README.md",
            "<!-- doc-content:documentation-index-table:start -->\n| Topic | Canonical source |\n| --- | --- |\n| Guidance | `docs/GUIDANCE.md` |\n<!-- doc-content:documentation-index-table:end -->\n\n<!-- doc-fact:documentation-index:start -->\n- current-requirement: `deprecated-docs-reviewed`\n- current-requirement: `guidance-centralized`\n<!-- doc-fact:documentation-index:end -->");

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Check_rejects_source_symlinks_outside_the_repository()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        using var outsideSource = new TemporaryTextDocument(DocumentationTestContent.SampleAggregateSource());
        workspace.WriteFile(ConformanceConfigPath, DocumentationTestContent.SwitchFactConformanceConfig());
        workspace.WriteFile(
            "docs/architecture/FLOWS.md",
            "<!-- doc-fact:sample-events:start -->\n- current-event: `FirstEvent`\n- current-event: `SecondEvent`\n<!-- doc-fact:sample-events:end -->");
        workspace.WriteFile("src/SampleProjection.cs", DocumentationTestContent.SampleProjectionSource());
        File.CreateSymbolicLink(Path.Combine(workspace.RootPath, "src", "SampleAggregate.cs"), outsideSource.Path);

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("symbolic link", StringComparison.OrdinalIgnoreCase);
        run.Error.ShouldContain("src/SampleAggregate.cs", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_reports_null_fact_configuration_collections()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(
            ConformanceConfigPath,
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

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("documentation-conformance.json", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_reports_mismatched_switch_sources_with_the_document_and_source_paths()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(ConformanceConfigPath, DocumentationTestContent.SwitchFactConformanceConfig());
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

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("docs/architecture/FLOWS.md", StringComparison.Ordinal);
        run.Error.ShouldContain("src/SampleProjection.cs", StringComparison.Ordinal);
    }

    [Fact]
    public void Check_reports_null_check_entries()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteFile(
            ConformanceConfigPath,
            """
            {
              "checks": [null]
            }
            """);

        // Act
        var run = DocumentationCommandLineRun.Invoke(workspace.RootPath, ["check", "--config", ConformanceConfigPath]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("documentation-conformance.json", StringComparison.Ordinal);
    }
}
