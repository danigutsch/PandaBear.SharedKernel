using System.Text.Json;
using System.Xml.Linq;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>Reads the lock file policy: the projects of the repository, which of them are lock-exempt, and their lock files.</summary>
internal static class LockFilePolicyFiles
{
    private static readonly string[] GeneratedDirectories = ["bin", "obj", ".git", ".worktrees", "artifacts", "node_modules"];

    /// <summary>Gets the repository root.</summary>
    public static string Root => SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();

    /// <summary>Gets the repository-relative project paths that <c>lockExemptProjects</c> lists, in file order.</summary>
    public static string[] ReadExemptProjects()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, ".config", "sharedkernel-repo.json")));
        return document.RootElement.GetProperty("lockExemptProjects").EnumerateArray().Select(project => project.GetString()!).ToArray();
    }

    /// <summary>Gets the repository-relative paths of every project file under the repository, outside generated directories.</summary>
    public static string[] ReadProjects() =>
        Enumerate(Root)
            .Where(path => path.EndsWith(".csproj", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>Indicates whether the directory of <paramref name="project"/> holds a <c>packages.lock.json</c>.</summary>
    public static bool HasLockFile(string project) =>
        File.Exists(Path.Combine(Root, Path.GetDirectoryName(project)!, "packages.lock.json"));

    /// <summary>Gets the <c>RestorePackagesWithLockFile</c> value that the project file itself sets, or <see langword="null"/>.</summary>
    public static string? ReadRestoreWithLockFile(string project) =>
        XDocument.Load(Path.Combine(Root, project)).Descendants("RestorePackagesWithLockFile").LastOrDefault()?.Value;

    private static IEnumerable<string> Enumerate(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            yield return file;
        }

        foreach (var child in Directory.EnumerateDirectories(directory).Where(child => !GeneratedDirectories.Contains(Path.GetFileName(child))))
        {
            foreach (var file in Enumerate(child))
            {
                yield return file;
            }
        }
    }
}
