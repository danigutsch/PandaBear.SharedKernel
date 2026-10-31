using System.Security.Cryptography;
using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Snapshot of the roadmap input files that detects any change between planning and writing.</summary>
internal sealed class RoadmapWriteInputSnapshot
{
    private readonly Dictionary<string, string> _files;
    private readonly string _rootPath;

    private RoadmapWriteInputSnapshot(string rootPath, Dictionary<string, string> files)
    {
        _rootPath = rootPath;
        _files = files;
        Digest = ComputeDigest();
    }

    /// <summary>Digest over the captured input paths and contents.</summary>
    public string Digest
    {
        get;
    }

    /// <summary>Reads all roadmap input files under <paramref name="rootPath"/> into a snapshot.</summary>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static RoadmapWriteInputSnapshot Capture(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var normalizedRoot = Path.GetFullPath(rootPath);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var files = EnumerateInputPaths(normalizedRoot)
            .ToDictionary(path => path, File.ReadAllText, comparer);
        return new RoadmapWriteInputSnapshot(normalizedRoot, files);
    }

    /// <summary>Returns the captured content of the file at <paramref name="path"/>, or null when it was not part of the snapshot.</summary>
    public string? GetExpectedContent(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return _files.TryGetValue(normalizedPath, out var content) ? content : null;
    }

    /// <summary>Throws when any input file was added, removed, or modified since capture.</summary>
    /// <exception cref="InvalidOperationException">An input file was added, removed, or modified since capture.</exception>
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

    private string ComputeDigest()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var file in _files.OrderBy(file => file.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString(
                    "path",
                    Path.GetRelativePath(_rootPath, file.Key).Replace(Path.DirectorySeparatorChar, '/'));
                writer.WriteString("content", file.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }
}
