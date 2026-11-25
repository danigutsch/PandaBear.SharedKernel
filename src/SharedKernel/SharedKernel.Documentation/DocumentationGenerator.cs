using System.Text.Json;

namespace SharedKernel.Documentation;

/// <summary>
/// Generates configured Markdown blocks from repository files.
/// </summary>
public static class DocumentationGenerator
{
    private const string MarkdownTocKind = "markdown-toc";

    /// <summary>
    /// Runs documentation generation from a JSON config file.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> or <paramref name="configPath"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <exception cref="InvalidOperationException">The configuration cannot be read or is invalid, or a block cannot be generated or applied.</exception>
    public static DocumentationGenerationResult Run(string rootPath, string configPath, bool checkOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        var fullRootPath = Path.GetFullPath(rootPath);
        var fullConfigPath = Path.GetFullPath(configPath, fullRootPath);
        var config = JsonSerializer.Deserialize(File.ReadAllText(fullConfigPath), DocumentationGeneratorJsonContext.Default.DocumentationGeneratorConfig)
            ?? throw new InvalidOperationException($"Could not read documentation generator config: {configPath}");
        ValidateConfig(config, configPath);

        var replacements = config.Blocks
            .Select(block => (block.Name, block.TargetPath, Render: CreateRenderer(fullRootPath, block)))
            .ToList();
        var updater = new GeneratedMarkdownUpdater(fullRootPath, config.DocsPath, config.GeneratorName);
        return new DocumentationGenerationResult(updater.Update(checkOnly, replacements));
    }

    /// <summary>Updates one generated Mermaid data ownership block or reports drift without writing.</summary>
    /// <param name="rootPath">The repository root.</param>
    /// <param name="docsPath">The repository-relative documentation folder.</param>
    /// <param name="generatorName">The name written in the generated marker's attribution.</param>
    /// <param name="blockName">The name in the existing generated-block markers.</param>
    /// <param name="targetPath">The existing Markdown document relative to the documentation folder.</param>
    /// <param name="diagram">The caller-owned trust boundary, node, and access facts.</param>
    /// <param name="checkOnly">Whether to report drift without changing the file.</param>
    /// <returns>The target document's relative path when it changed or was stale.</returns>
    /// <exception cref="ArgumentNullException">A required argument, diagram collection, or diagram item is null.</exception>
    /// <exception cref="ArgumentException">A required path or name is blank, or the diagram is invalid.</exception>
    /// <exception cref="InvalidOperationException">The configured path or generated-block markers are invalid.</exception>
    public static DocumentationGenerationResult RunDataOwnership(
        string rootPath, string docsPath, string generatorName, string blockName, string targetPath,
        DataOwnershipDiagram diagram, bool checkOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(docsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        ArgumentNullException.ThrowIfNull(diagram);

        var replacement = DataOwnershipMermaidDiagram.Build(diagram);
        var updater = new GeneratedMarkdownUpdater(Path.GetFullPath(rootPath), docsPath, generatorName);
        return new DocumentationGenerationResult(updater.Update(checkOnly, [(blockName, targetPath, _ => replacement)]));
    }

    private static void ValidateConfig(DocumentationGeneratorConfig config, string configPath)
    {
        if (string.IsNullOrWhiteSpace(config.DocsPath))
        {
            throw new InvalidOperationException("Missing required docsPath.");
        }

        if (string.IsNullOrWhiteSpace(config.GeneratorName))
        {
            throw new InvalidOperationException("Missing required generatorName.");
        }

        if (config.Blocks is not { Count: > 0 })
        {
            throw new InvalidOperationException("Missing required blocks.");
        }

        for (var blockIndex = 0; blockIndex < config.Blocks.Count; blockIndex++)
        {
            var block = config.Blocks[blockIndex] ?? throw new InvalidOperationException(
                $"Block at index {blockIndex} in documentation generator config '{configPath}' must not be null.");

            if (block.Kind == MarkdownTocKind && (block.MinHeadingLevel < 1 || block.MaxHeadingLevel > 6 || block.MinHeadingLevel > block.MaxHeadingLevel))
            {
                throw new InvalidOperationException(
                    $"Generated block '{block.Name}' requires minHeadingLevel and maxHeadingLevel from 1 through 6 in ascending order.");
            }
        }

        if (config.Blocks.Select(block => block.Name).Distinct(StringComparer.Ordinal).Count() != config.Blocks.Count)
        {
            throw new InvalidOperationException("Generated block names must be unique.");
        }

        var blockWithoutTarget = config.Blocks.FirstOrDefault(block => string.IsNullOrWhiteSpace(block.TargetPath));
        if (blockWithoutTarget is not null)
        {
            throw new InvalidOperationException(
                $"Generated block '{blockWithoutTarget.Name}' is missing required targetPath.");
        }
    }

    private static Func<string, string> CreateRenderer(string rootPath, DocumentationGeneratorBlock block)
    {
        if (block.Kind == MarkdownTocKind)
        {
            return markdown => MarkdownTableOfContents.Build(markdown, block.MinHeadingLevel, block.MaxHeadingLevel);
        }

        var replacement = GenerateBlock(rootPath, block);
        return _ => replacement;
    }

    private static string GenerateBlock(string rootPath, DocumentationGeneratorBlock block) => block.Kind switch
    {
        "mermaid-flowchart" => MermaidDiagram.Build(block.Flowchart, block.Lines),
        "project-references" => ProjectReferenceMermaidDiagram.Build(rootPath, block.SourcePath, ProjectFilter(block.ProjectFilter)),
        "apphost-resources" => AppHostMermaidDiagram.Build(Path.Combine(rootPath, block.SourcePath), block.Labels),
        "endpoint-routes" => EndpointRouteMarkdownTable.Build(Path.Combine(rootPath, block.SourcePath), block,
            OpenApiRouteAuthorization.Load(rootPath, block.OpenApiAuthorizationDocuments)),
        "integration-events" => IntegrationEventMarkdownTable.Build(Path.Combine(rootPath, block.SourcePath)),
        "mediator-topology" => MediatorTopologyMermaidDiagram.Build(Path.Combine(rootPath, block.SourcePath)),
        "github-actions-jobs" => GitHubActionsMermaidDiagram.BuildJobs(Path.Combine(rootPath, block.SourcePath), block.TriggerLabel),
        "github-actions-workflows" => GitHubActionsMermaidDiagram.BuildWorkflowInventory(Path.Combine(rootPath, block.SourcePath), block.RootLabel, block.ExcludedWorkflowFileName),
        _ => throw new InvalidOperationException($"Unknown generated documentation block kind: {block.Kind}"),
    };

    private static Func<string[], bool> ProjectFilter(string value) => value switch
    {
        "src-excluding-sharedkernel" => path => path[0] == "src" && !path.Contains("SharedKernel", StringComparer.Ordinal),
        "sharedkernel" => path => path is ["src", "SharedKernel", ..],
        _ => throw new InvalidOperationException($"Unknown project filter: {value}"),
    };
}
