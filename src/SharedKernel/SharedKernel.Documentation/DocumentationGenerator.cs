using System.Text.Json;

namespace PandaBear.Documentation;

/// <summary>
/// Generates configured Markdown blocks from repository files.
/// </summary>
public static class DocumentationGenerator
{
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
            .Select(block => (block.Name, block.TargetPath, Replacement: GenerateBlock(fullRootPath, block)))
            .ToList();
        var updater = new GeneratedMarkdownUpdater(fullRootPath, config.DocsPath, config.GeneratorName);
        return new DocumentationGenerationResult(updater.Update(checkOnly, replacements));
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
            if (config.Blocks[blockIndex] is null)
            {
                throw new InvalidOperationException(
                    $"Block at index {blockIndex} in documentation generator config '{configPath}' must not be null.");
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

    private static string GenerateBlock(string rootPath, DocumentationGeneratorBlock block) => block.Kind switch
    {
        "mermaid-flowchart" => MermaidDiagram.Build(block.Flowchart, block.Lines),
        "project-references" => ProjectReferenceMermaidDiagram.Build(rootPath, block.SourcePath, ProjectFilter(block.ProjectFilter)),
        "apphost-resources" => AppHostMermaidDiagram.Build(Path.Combine(rootPath, block.SourcePath), block.Labels),
        "endpoint-routes" => EndpointRouteMarkdownTable.Build(Path.Combine(rootPath, block.SourcePath), block),
        "integration-events" => IntegrationEventMarkdownTable.Build(Path.Combine(rootPath, block.SourcePath)),
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
