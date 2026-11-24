namespace PandaBear.Documentation.Tests;

/// <summary>Exercises the generated mediator topology through the documentation generator.</summary>
[Trait(Testing.TestTraitNames.CapabilityName, TestTraits.DocumentationGenerationCapability)]
[Trait(Testing.TestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class MediatorDocumentationTests
{
    private const string ConfigurationPath = "docs/architecture/generated-diagrams.json";
    private const string TopologyPath = "obj/generated/SharedKernel.Mediator.Generated.CallGraph.g.cs";
    private const string GraphTemplate = """"
        namespace PandaBear.Mediator.Generated;
        internal static class MediatorCallGraph
        {
            public static string Json { get; } = """$GRAPH""";
        }
        """";
    private const string Config = $$"""
        {
          "docsPath": "docs/architecture",
          "generatorName": "test-generator",
          "blocks": [{
            "name": "sample",
            "kind": "mediator-topology",
            "sourcePath": "{{TopologyPath}}",
            "targetPath": "overview.md"
          }]
        }
        """;

    private const string Graph = """"
        namespace PandaBear.Mediator.Generated;
        internal static class MediatorCallGraph
        {
            public static string Json { get; } =
                """
                {
                  "schemaVersion": 1,
                  "requests": [
                    {
                      "request": "global::Demo.Create",
                      "kind": "command",
                      "response": "Unit",
                      "handler": "global::Demo.CreateHandler",
                      "pipelines": ["global::Demo.Validation", "global::Demo.Transaction"]
                    }
                  ],
                  "notifications": [
                    {
                      "notification": "global::Demo.Created",
                      "handlers": ["global::Demo.Audit", "global::Demo.Projection<global::Demo.Created>"],
                      "dispatch": "parallel"
                    }
                  ],
                  "streamRequests": [
                    {
                      "request": "global::Demo.Read",
                      "response": "string",
                      "handler": "<none>",
                      "pipelines": []
                    }
                  ]
                }
                """;
        }
        """";

    [Fact]
    public void Run_generates_ordered_mediator_topology_from_compiler_output()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteFile(TopologyPath, Graph);
        workspace.WriteConfig(Config);

        // Act
        DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: false);
        var updated = workspace.ReadArchitectureDoc("overview.md");
        var verified = DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: true);

        // Assert
        updated.ShouldContain("command Demo.Create", StringComparison.Ordinal);
        updated.ShouldContain("Validation", StringComparison.Ordinal);
        updated.ShouldContain("Transaction", StringComparison.Ordinal);
        updated.ShouldContain("CreateHandler", StringComparison.Ordinal);
        updated.IndexOf("Validation", StringComparison.Ordinal).ShouldBeLessThan(updated.IndexOf("Transaction", StringComparison.Ordinal));
        updated.ShouldContain("notification Demo.Created (parallel)", StringComparison.Ordinal);
        updated.ShouldContain("Audit", StringComparison.Ordinal);
        updated.ShouldContain("Demo.Projection#60;Demo.Created#62;", StringComparison.Ordinal);
        updated.ShouldContain("stream Demo.Read", StringComparison.Ordinal);
        updated.ShouldContain("No unique handler", StringComparison.Ordinal);
        verified.ChangedFiles.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"schemaVersion": "1"}""")]
    [InlineData("""{"schemaVersion": 1.5}""")]
    [InlineData("""{"schemaVersion": 9}""")]
    public void Run_reports_an_unsupported_mediator_topology_version(string json)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(Config);
        workspace.WriteFile(TopologyPath, GraphTemplate.Replace("$GRAPH", json, StringComparison.Ordinal));
        Action act = () => DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: true);

        // Act
        var failure = act.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("Unsupported mediator call graph schema", StringComparison.Ordinal);
        failure.Message.ShouldContain(TopologyPath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"schemaVersion": 1}""")]
    [InlineData("""{"schemaVersion": 1, "requests": true}""")]
    [InlineData("""{"schemaVersion": 1, "requests": [{"kind": null}]}""")]
    [InlineData("""{"schemaVersion": 1, "requests": [{"kind": "query", "request": "Find", "pipelines": [null]}]}""")]
    [InlineData("""{"schemaVersion": 1, "requests": [], "notifications": [{"notification": "Event", "dispatch": "parallel", "handlers": [null]}]}""")]
    public void Run_reports_malformed_mediator_graph_JSON(string json)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(Config);
        workspace.WriteFile(TopologyPath, GraphTemplate.Replace("$GRAPH", json, StringComparison.Ordinal));
        Action act = () => DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: false);

        // Act
        var failure = act.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("Invalid mediator call graph JSON", StringComparison.Ordinal);
        failure.Message.ShouldContain(TopologyPath, StringComparison.Ordinal);
        workspace.ReadArchitectureDoc("overview.md").ShouldContain("old[Old]", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("namespace PandaBear.Mediator.Generated;")]
    [InlineData("namespace PandaBear.Mediator.Generated; internal static class MediatorCallGraph { public static string Json { get; } = Other.Json; }")]
    public void Run_requires_a_generated_mediator_graph_literal(string source)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(Config);
        workspace.WriteFile(TopologyPath, source);
        Action act = () => DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: true);

        // Act
        var failure = act.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("Missing the generated mediator call graph JSON", StringComparison.Ordinal);
        failure.Message.ShouldContain(TopologyPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_renders_an_empty_mediator_graph()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        workspace.WriteConfig(Config);
        const string emptyGraph = """{"schemaVersion": 1, "requests": [], "notifications": [], "streamRequests": []}""";
        workspace.WriteFile(TopologyPath, GraphTemplate.Replace("$GRAPH", emptyGraph, StringComparison.Ordinal));

        // Act
        DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: false);
        var updated = workspace.ReadArchitectureDoc("overview.md");

        // Assert
        updated.ShouldContain("No mediator contracts", StringComparison.Ordinal);
    }
}
