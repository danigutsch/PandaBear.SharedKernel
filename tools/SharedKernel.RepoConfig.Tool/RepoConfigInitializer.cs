namespace PandaBear.RepoConfig.Tool;

/// <summary>Creates the default roadmap configuration files.</summary>
internal static class RepoConfigInitializer
{
    /// <summary>Creates the missing default files and returns the relative paths created.</summary>
    /// <param name="rootPath">Repository root.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> is <see langword="null"/>, empty, or white-space.</exception>
    public static IReadOnlyList<string> Initialize(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var files = new (string Path, string Content)[]
        {
            (RepoConfigPaths.RoadmapReadme, RoadmapTemplates.RoadmapReadme),
            (RepoConfigPaths.Config, RoadmapTemplates.ConfigJson),
            (RepoConfigPaths.Order, RoadmapTemplates.OrderJson),
            (RepoConfigPaths.ConfigSchema, RoadmapTemplates.ConfigSchemaJson),
            (RepoConfigPaths.ItemSchema, RoadmapTemplates.ItemSchemaJson),
            (RepoConfigPaths.DefaultTheme, RoadmapTemplates.DefaultThemeJson),
            (RepoConfigPaths.DefaultItem, RoadmapTemplates.DefaultItemJson)
        };
        foreach (var relativePath in RepoConfigPaths.RequiredDirectories.Concat(files.Select(file => file.Path)))
        {
            RollbackFileWriteBatch.ValidateWritePath(rootPath, Path.Combine(rootPath, relativePath));
        }

        List<string> createdPaths = [];
        foreach (var directory in RepoConfigPaths.RequiredDirectories)
        {
            var fullPath = Path.Combine(rootPath, directory);
            if (Directory.Exists(fullPath))
            {
                continue;
            }

            RollbackFileWriteBatch.ValidateWritePath(rootPath, fullPath);
            Directory.CreateDirectory(fullPath);
            createdPaths.Add(RepoConfigPaths.Normalize(directory));
        }

        var writes = files.Where(file => !File.Exists(Path.Combine(rootPath, file.Path)))
            .Select(file => new AtomicFileWrite(Path.Combine(rootPath, file.Path), file.Content, null))
            .ToArray();
        RollbackFileWriteBatch.Apply(rootPath, writes);
        createdPaths.AddRange(writes.Select(write => RepoConfigPaths.RelativeTo(rootPath, write.Path)));
        return createdPaths;
    }
}
