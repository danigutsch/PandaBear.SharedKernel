namespace PandaBear.Repositories;

/// <summary>Creates the default roadmap configuration files.</summary>
public static class RoadmapInitializer
{
    /// <summary>Creates the missing default files and returns the relative paths created.</summary>
    /// <param name="rootPath">Repository root.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> is <see langword="null"/>, empty, or white-space.</exception>
    public static IReadOnlyList<string> Initialize(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var files = new (string Path, string Content)[]
        {
            (RoadmapPaths.RoadmapReadme, RoadmapTemplates.RoadmapReadme),
            (RoadmapPaths.Config, RoadmapTemplates.ConfigJson),
            (RoadmapPaths.Order, RoadmapTemplates.OrderJson),
            (RoadmapPaths.ConfigSchema, RoadmapTemplates.ConfigSchemaJson),
            (RoadmapPaths.ItemSchema, RoadmapTemplates.ItemSchemaJson),
            (RoadmapPaths.DefaultTheme, RoadmapTemplates.DefaultThemeJson),
            (RoadmapPaths.DefaultItem, RoadmapTemplates.DefaultItemJson)
        };
        foreach (var relativePath in RoadmapPaths.RequiredDirectories.Concat(files.Select(file => file.Path)))
        {
            RollbackFileWriteBatch.ValidateWritePath(rootPath, Path.Combine(rootPath, relativePath));
        }

        List<string> createdPaths = [];
        foreach (var directory in RoadmapPaths.RequiredDirectories)
        {
            var fullPath = Path.Combine(rootPath, directory);
            if (Directory.Exists(fullPath))
            {
                continue;
            }

            RollbackFileWriteBatch.ValidateWritePath(rootPath, fullPath);
            Directory.CreateDirectory(fullPath);
            createdPaths.Add(RoadmapPaths.Normalize(directory));
        }

        var writes = files.Where(file => !File.Exists(Path.Combine(rootPath, file.Path)))
            .Select(file => new AtomicFileWrite(Path.Combine(rootPath, file.Path), file.Content, null))
            .ToArray();
        RollbackFileWriteBatch.Apply(rootPath, writes);
        createdPaths.AddRange(writes.Select(write => RoadmapPaths.RelativeTo(rootPath, write.Path)));
        return createdPaths;
    }
}
