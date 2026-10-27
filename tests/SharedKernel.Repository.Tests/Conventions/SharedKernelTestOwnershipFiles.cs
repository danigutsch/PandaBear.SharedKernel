using System.Xml.Linq;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>Computes which SharedKernel projects the test projects reach through project references.</summary>
internal static class SharedKernelTestOwnershipFiles
{
    private static readonly string[] SearchedDirectories = ["src", "tests"];

    /// <summary>Follows project references transitively from every xunit test project under tests and returns the sorted names of the packable src/SharedKernel projects that none of them reaches.</summary>
    public static string[] FindUnreachedPackableProjects()
    {
        var repositoryRoot = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var projects = SearchedDirectories
            .SelectMany(directory => Directory.GetFiles(Path.Combine(repositoryRoot, directory), "*.csproj", SearchOption.AllDirectories))
            .ToDictionary(Path.GetFullPath, ReadReferences, StringComparer.Ordinal);
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(projects.Where(static project => project.Value.IsTestProject).Select(static project => project.Key));

        while (pending.TryPop(out var project))
        {
            if (reached.Add(project) && projects.TryGetValue(project, out var node))
            {
                foreach (var reference in node.References)
                {
                    pending.Push(reference);
                }
            }
        }

        return projects.Keys
            .Where(path => IsUnder(repositoryRoot, path, Path.Combine("src", "SharedKernel")))
            .Where(path => !string.Equals(SharedKernelPackagingConventionTestFiles.GetProperty(path, "IsPackable"), "false", StringComparison.OrdinalIgnoreCase))
            .Where(path => !reached.Contains(path))
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsUnder(string repositoryRoot, string path, string directory) =>
        Path.GetFullPath(path).StartsWith(Path.Combine(repositoryRoot, directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static (bool IsTestProject, string[] References) ReadReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        var references = document.Descendants("ProjectReference")
            .Select(static reference => reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar))
            .Select(reference => Path.GetFullPath(Path.Combine(directory, reference)))
            .ToArray();
        var isTestProject = projectPath.Contains(Path.DirectorySeparatorChar + "tests" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && document.Descendants("PackageReference").Any(static package => package.Attribute("Include")?.Value == "xunit.v3.mtp-v2");
        return (isTestProject, references);
    }
}
