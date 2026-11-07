namespace PandaBear.Versioning.Tests;

/// <summary>A throwaway SharedKernel-shaped repository with project files only, for planning tests.</summary>
internal sealed class DevPackRepository : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("sk-devpack-");

    /// <summary>Gets the repository root.</summary>
    internal string Root => root.FullName;

    /// <summary>Gets the feed directory used by the test.</summary>
    internal string Feed => Path.Combine(root.FullName, "feed");

    /// <summary>Adds <c>src/SharedKernel/&lt;name&gt;/&lt;name&gt;.csproj</c>.</summary>
    /// <param name="name">The project name.</param>
    /// <param name="packageId">The <c>PackageId</c> to write, or <see langword="null"/> for none.</param>
    /// <param name="references">Names of projects to reference.</param>
    /// <returns>The project path.</returns>
    internal string AddProject(string name, string? packageId = null, params string[] references)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root.FullName, "src", "SharedKernel", name));
        var id = packageId is null ? string.Empty : $"<PropertyGroup><PackageId>{packageId}</PackageId></PropertyGroup>";
        var items = string.Concat(references.Select(reference => $"<ProjectReference Include=\"..\\{reference}\\{reference}.csproj\" />"));
        var path = Path.Combine(directory.FullName, name + ".csproj");
        File.WriteAllText(path, $"<Project Sdk=\"Microsoft.NET.Sdk\">{id}<ItemGroup>{items}</ItemGroup></Project>");
        return path;
    }

    /// <summary>Adds a project reference that points outside the known SharedKernel projects.</summary>
    /// <param name="name">The project that gets the reference.</param>
    internal void AddExternalReference(string name)
    {
        var path = Path.Combine(root.FullName, "src", "SharedKernel", name, name + ".csproj");
        File.WriteAllText(path, File.ReadAllText(path).Replace("</ItemGroup>", "<ProjectReference Include=\"..\\..\\..\\Other\\Other.csproj\" /></ItemGroup>", StringComparison.Ordinal));
    }

    /// <summary>Puts a package for the project into the feed, as an earlier pack would have.</summary>
    /// <param name="packageId">The package id.</param>
    /// <param name="version">The package version.</param>
    internal void AddFeedPackage(string packageId, string version)
    {
        Directory.CreateDirectory(Feed);
        File.WriteAllText(Path.Combine(Feed, packageId + "." + version + ".nupkg"), string.Empty);
    }

    /// <summary>Adds a file to a project directory with the given last-write time.</summary>
    /// <param name="project">The project name.</param>
    /// <param name="relativePath">The path relative to the project directory.</param>
    /// <param name="lastWriteUtc">The last-write time.</param>
    internal void AddProjectFile(string project, string relativePath, DateTime lastWriteUtc)
    {
        var path = Path.Combine(root.FullName, "src", "SharedKernel", project, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "// source");
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
    }

    /// <summary>Sets the last-write time of every file of a project, its project file included.</summary>
    /// <param name="project">The project name.</param>
    /// <param name="lastWriteUtc">The last-write time.</param>
    internal void SetProjectFileTimes(string project, DateTime lastWriteUtc)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root.FullName, "src", "SharedKernel", project), "*", SearchOption.AllDirectories))
        {
            File.SetLastWriteTimeUtc(file, lastWriteUtc);
        }
    }

    /// <summary>Sets the last-write time of a package in the feed.</summary>
    /// <param name="packageId">The package id.</param>
    /// <param name="version">The package version.</param>
    /// <param name="lastWriteUtc">The last-write time.</param>
    internal void SetFeedPackageTime(string packageId, string version, DateTime lastWriteUtc) =>
        File.SetLastWriteTimeUtc(Path.Combine(Feed, packageId + "." + version + ".nupkg"), lastWriteUtc);

    /// <inheritdoc />
    public void Dispose() => root.Delete(recursive: true);
}
