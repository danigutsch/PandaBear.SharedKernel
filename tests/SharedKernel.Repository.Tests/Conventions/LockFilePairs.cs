using System.Text.Json;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>Reads the resolved <c>Id@Version</c> pairs of lock files and restored assets for the lock file policy tests.</summary>
internal static class LockFilePairs
{
    /// <summary>Reads the resolved pairs of the <c>packages.lock.json</c> next to <paramref name="project"/>, or an empty set when it has none.</summary>
    public static HashSet<string> ReadLocked(string project)
    {
        var pairs = NewSet();
        var lockFile = Path.Combine(LockFilePolicyFiles.Root, Path.GetDirectoryName(project)!, "packages.lock.json");
        if (!File.Exists(lockFile))
        {
            return pairs;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(lockFile));
        foreach (var framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            foreach (var package in framework.Value.EnumerateObject())
            {
                if (package.Value.TryGetProperty("resolved", out var resolved))
                {
                    pairs.Add(package.Name + "@" + resolved.GetString());
                }
            }
        }

        return pairs;
    }

    /// <summary>Reads the resolved pairs of the restored <c>obj/project.assets.json</c> of <paramref name="project"/>, or <see langword="null"/> when the project was not restored.</summary>
    public static HashSet<string>? ReadRestored(string project)
    {
        var assets = Path.Combine(LockFilePolicyFiles.Root, Path.GetDirectoryName(project)!, "obj", "project.assets.json");
        if (!File.Exists(assets))
        {
            return null;
        }

        var pairs = NewSet();
        using var document = JsonDocument.Parse(File.ReadAllText(assets));
        foreach (var target in document.RootElement.GetProperty("targets").EnumerateObject())
        {
            foreach (var entry in target.Value.EnumerateObject())
            {
                if (entry.Value.TryGetProperty("type", out var type) && type.GetString() == "package")
                {
                    pairs.Add(entry.Name.Replace('/', '@'));
                }
            }
        }

        return pairs;
    }

    private static HashSet<string> NewSet() => new(StringComparer.OrdinalIgnoreCase);
}
