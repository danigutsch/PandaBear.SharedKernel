namespace SharedKernel.RepoConfig.Tool;

internal sealed class RoadmapWriteInputSnapshot
{
    private readonly Dictionary<string, string> _files;
    private readonly string _rootPath;

    private RoadmapWriteInputSnapshot(string rootPath, Dictionary<string, string> files)
    {
        _rootPath = rootPath;
        _files = files;
    }

    public static RoadmapWriteInputSnapshot Capture(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var normalizedRoot = Path.GetFullPath(rootPath);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var files = EnumerateInputPaths(normalizedRoot)
            .ToDictionary(path => path, File.ReadAllText, comparer);
        return new RoadmapWriteInputSnapshot(normalizedRoot, files);
    }

    public string? GetExpectedContent(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return _files.TryGetValue(normalizedPath, out var content) ? content : null;
    }

    public void Verify()
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var currentPaths = EnumerateInputPaths(_rootPath).ToHashSet(comparer);
        var expectedPaths = _files.Keys.ToHashSet(comparer);
        var changedPath = currentPaths.Except(expectedPaths, comparer)
            .Concat(expectedPaths.Except(currentPaths, comparer))
            .Order(comparer)
            .FirstOrDefault();
        if (changedPath is null)
        {
            foreach (var file in _files)
            {
                if (!File.Exists(file.Key))
                {
                    changedPath = file.Key;
                    break;
                }

                try
                {
                    if (string.Equals(File.ReadAllText(file.Key), file.Value, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    changedPath = file.Key;
                    break;
                }

                changedPath = file.Key;
                break;
            }
        }

        if (changedPath is not null)
        {
            throw new InvalidOperationException($"File changed after the write plan was created: {changedPath}.");
        }
    }

    private static IEnumerable<string> EnumerateInputPaths(string rootPath)
    {
        yield return Path.GetFullPath(Path.Combine(rootPath, RepoConfigPaths.Config));
        yield return Path.GetFullPath(Path.Combine(rootPath, RepoConfigPaths.Order));

        foreach (var directory in new[] { RepoConfigPaths.Items, RepoConfigPaths.Themes, RepoConfigPaths.Reconciliation })
        {
            var path = Path.Combine(rootPath, directory);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
            {
                yield return Path.GetFullPath(file);
            }
        }
    }
}
