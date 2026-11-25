namespace PandaBear.Documentation.Tests;

/// <summary>Tests generated Markdown contents and heading-range validation.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentationGenerationCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class MarkdownTableOfContentsTests
{
    private const string ConfigurationPath = "docs/architecture/generated-diagrams.json";
    private const string Config = """
        {
          "docsPath": "docs/architecture",
          "generatorName": "test-generator",
          "blocks": [{
            "name": "sample",
            "kind": "markdown-toc",
            "targetPath": "overview.md",
            "minHeadingLevel": 2,
            "maxHeadingLevel": 3
          }]
        }
        """;

    [Fact]
    public void Run_updates_a_contents_block_from_unfenced_headings()
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        const string source = """
            # Overview

            <!-- generated:sample:start -->
            ## Stale heading
            <!-- generated:sample:end -->

            ## Guide
            ### First step
            ~~~text
            ## Example heading
            ~~~
            ## Guide
            ## São [Paulo]
            ## 🙂
            #### Deeper detail
            """;
        workspace.WriteArchitectureDoc("overview.md", source);
        workspace.WriteConfig(Config);

        // Act
        var stale = DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: true);
        DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: false);
        var updated = workspace.ReadArchitectureDoc("overview.md");
        var current = DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: true);

        // Assert
        stale.ChangedFiles.ShouldContain("docs/architecture/overview.md");
        updated.ShouldContain("- [Guide](#guide)", StringComparison.Ordinal);
        updated.ShouldContain("  - [First step](#first-step)", StringComparison.Ordinal);
        updated.ShouldContain("- [Guide](#guide-1)", StringComparison.Ordinal);
        updated.ShouldContain("- [São \\[Paulo\\]](#s%C3%A3o-paulo)", StringComparison.Ordinal);
        updated.ShouldNotContain("- [🙂]", StringComparison.Ordinal);
        updated.ShouldNotContain("#example-heading", StringComparison.Ordinal);
        updated.ShouldNotContain("#deeper-detail", StringComparison.Ordinal);
        updated.ShouldNotContain("#stale-heading", StringComparison.Ordinal);
        updated.ShouldNotContain("#overview", StringComparison.Ordinal);
        current.ChangedFiles.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(2, 7)]
    [InlineData(4, 3)]
    public void Run_rejects_an_invalid_contents_heading_range(int minHeadingLevel, int maxHeadingLevel)
    {
        // Arrange
        using var workspace = new TemporaryDocumentationWorkspace();
        workspace.WriteArchitectureDoc("overview.md", DocumentationTestContent.GeneratedBlockDocument("old[Old]"));
        var config = Config.Replace("\"minHeadingLevel\": 2", $"\"minHeadingLevel\": {minHeadingLevel}", StringComparison.Ordinal)
            .Replace("\"maxHeadingLevel\": 3", $"\"maxHeadingLevel\": {maxHeadingLevel}", StringComparison.Ordinal);
        workspace.WriteConfig(config);
        Action act = () => DocumentationGenerator.Run(workspace.RootPath, ConfigurationPath, checkOnly: false);

        // Act
        var failure = act.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("minHeadingLevel and maxHeadingLevel", StringComparison.Ordinal);
        workspace.ReadArchitectureDoc("overview.md").ShouldContain("old[Old]", StringComparison.Ordinal);
    }
}
