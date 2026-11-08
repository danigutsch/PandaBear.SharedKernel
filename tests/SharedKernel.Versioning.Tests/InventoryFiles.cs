namespace PandaBear.Versioning.Tests;

/// <summary>Writes lock files, project assets files, and project files for package inventory tests.</summary>
internal static class InventoryFiles
{
    /// <summary>Writes <c>packages.lock.json</c> in <paramref name="directory"/> (relative to the root) resolving the given <c>id@version</c> pairs.</summary>
    public static async Task WriteLock(string root, string directory, params string[] packages)
    {
        var entries = string.Join(",", packages.Select(package =>
        {
            var (id, version) = Split(package, '@');
            return $"\"{id}\":{{\"type\":\"Transitive\",\"resolved\":\"{version}\"}}";
        }));
        await Write(root, directory + "/packages.lock.json", $"{{\"version\":2,\"dependencies\":{{\"net10.0\":{{{entries}}}}}}}");
    }

    /// <summary>Writes <c>obj/project.assets.json</c> in <paramref name="directory"/> restoring the given <c>id@version</c> pairs, plus a project reference.</summary>
    public static async Task WriteAssets(string root, string directory, params string[] packages)
    {
        var entries = string.Join(",", packages.Select(package =>
        {
            var (id, version) = Split(package, '@');
            return $"\"{id}/{version}\":{{\"type\":\"package\"}}";
        }));
        await Write(root, directory + "/obj/project.assets.json", $"{{\"version\":3,\"targets\":{{\"net10.0\":{{{entries},\"Other.Project/1.0.0\":{{\"type\":\"project\"}}}}}}}}");
    }

    /// <summary>Writes an empty project file named after the directory.</summary>
    public static Task WriteProject(string root, string directory) =>
        Write(root, directory + "/" + Path.GetFileName(directory) + ".csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

    private static (string Left, string Right) Split(string value, char separator)
    {
        var index = value.LastIndexOf(separator);
        return (value[..index], value[(index + 1)..]);
    }

    private static async Task Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
    }
}
