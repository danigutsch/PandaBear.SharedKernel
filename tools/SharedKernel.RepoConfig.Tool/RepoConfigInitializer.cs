namespace SharedKernel.RepoConfig.Tool;

internal static class RepoConfigInitializer
{
    public static IReadOnlyList<string> Initialize(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        List<string> createdPaths = [];
        foreach (var directory in RepoConfigPaths.RequiredDirectories)
        {
            var fullPath = Path.Combine(rootPath, directory);
            if (Directory.Exists(fullPath))
            {
                continue;
            }

            Directory.CreateDirectory(fullPath);
            createdPaths.Add(RepoConfigPaths.Normalize(directory));
        }

        WriteMissingFile(rootPath, RepoConfigPaths.RoadmapReadme, RoadmapTemplates.RoadmapReadme, createdPaths);
        WriteMissingFile(rootPath, RepoConfigPaths.Config, RoadmapTemplates.ConfigJson, createdPaths);
        WriteMissingFile(rootPath, RepoConfigPaths.Order, RoadmapTemplates.OrderJson, createdPaths);
        WriteMissingFile(rootPath, RepoConfigPaths.ConfigSchema, RoadmapTemplates.ConfigSchemaJson, createdPaths);
        WriteMissingFile(rootPath, RepoConfigPaths.ItemSchema, RoadmapTemplates.ItemSchemaJson, createdPaths);
        WriteMissingFile(rootPath, RepoConfigPaths.DefaultTheme, RoadmapTemplates.DefaultThemeJson, createdPaths);
        WriteMissingFile(rootPath, RepoConfigPaths.DefaultItem, RoadmapTemplates.DefaultItemJson, createdPaths);

        return createdPaths;
    }

    private static void WriteMissingFile(string rootPath, string relativePath, string content, List<string> createdPaths)
    {
        var fullPath = Path.Combine(rootPath, relativePath);
        if (File.Exists(fullPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? rootPath);
        File.WriteAllText(fullPath, content);
        createdPaths.Add(RepoConfigPaths.Normalize(relativePath));
    }
}
