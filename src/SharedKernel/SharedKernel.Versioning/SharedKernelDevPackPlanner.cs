using System.IO.Enumeration;
using System.Xml.Linq;

namespace PandaBear.Versioning;

/// <summary>
/// Plans a fast, incremental local pack of SharedKernel for a consumer that develops against uncommitted library changes:
/// the named projects are always rebuilt, their project-reference dependencies only when the feed does not hold them yet.
/// </summary>
public static class SharedKernelDevPackPlanner
{
    /// <summary>Decides which projects to pack, dependencies first.</summary>
    /// <param name="repoRoot">The SharedKernel repository root.</param>
    /// <param name="requested">Project names to rebuild, for example <c>SharedKernel.Results</c>; empty rebuilds every project.</param>
    /// <param name="feedDirectory">The feed directory that receives the packages.</param>
    /// <param name="version">The package version.</param>
    /// <returns>The steps in dependency order.</returns>
    /// <exception cref="ArgumentException">An argument is blank, or a requested project does not exist.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="requested"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<DevPackStep> Plan(string repoRoot, IReadOnlyCollection<string> requested, string feedDirectory, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentException.ThrowIfNullOrWhiteSpace(feedDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var projectsByName = SharedKernelPackagePlanner.FindProjects(repoRoot)
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase);
        var roots = requested.Count == 0 ? projectsByName.Keys.Order(StringComparer.Ordinal).ToList() : ResolveRequested(requested, projectsByName);
        var forced = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);

        var ordered = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            Visit(root, projectsByName, visited, ordered);
        }

        return ordered.Select(name =>
        {
            var projectPath = projectsByName[name];
            var packageId = ReadPackageId(projectPath) ?? name;
            var package = Path.Combine(feedDirectory, packageId + "." + version + ".nupkg");
            var present = File.Exists(package);
            var rebuild = forced.Contains(name) || !present;
            return new DevPackStep(name, projectPath, packageId, rebuild, !rebuild && SourcesChangedSince(projectPath, File.GetLastWriteTimeUtc(package)));
        }).ToList();
    }

    private static bool SourcesChangedSince(string projectPath, DateTime packedAtUtc)
    {
        var directory = Path.GetDirectoryName(projectPath)!;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        return new FileSystemEnumerable<bool>(
            directory,
            (ref FileSystemEntry entry) => true,
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                !entry.IsDirectory
                && SourceExtensions.Contains(Path.GetExtension(entry.FileName).ToString())
                && entry.LastWriteTimeUtc.UtcDateTime > packedAtUtc
                && !IsBuildOutput(entry.Directory.ToString(), directory),
            ShouldRecursePredicate = (ref FileSystemEntry entry) => !IsBuildOutputName(entry.FileName),
        }.Any();
    }

    private static bool IsBuildOutput(string entryDirectory, string projectDirectory) =>
        Path.GetRelativePath(projectDirectory, entryDirectory)
            .Split(Path.DirectorySeparatorChar)
            .Any(segment => IsBuildOutputName(segment));

    private static bool IsBuildOutputName(ReadOnlySpan<char> name) =>
        name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase);

    private static readonly HashSet<string> SourceExtensions = new([".cs", ".csproj", ".props", ".targets"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Removes a package version from the NuGet global packages folder, which caches by id and version and would otherwise keep serving the previous build.</summary>
    /// <param name="packageId">The package id.</param>
    /// <param name="version">The package version.</param>
    /// <param name="globalPackagesDirectory">The global packages folder; default: <c>NUGET_PACKAGES</c> when not empty, else <c>~/.nuget/packages</c>. Use <see cref="ResolveGlobalPackagesDirectory"/> to honor <c>NuGet.Config</c>.</param>
    /// <returns><see langword="true"/> when a cached copy was removed.</returns>
    /// <exception cref="ArgumentException">An argument is blank or contains a path separator or <c>..</c>.</exception>
    public static bool EvictFromGlobalPackages(string packageId, string version, string? globalPackagesDirectory = null)
    {
        EnsureSingleSegment(packageId);
        EnsureSingleSegment(version);

        var root = FirstNonBlank(globalPackagesDirectory, Environment.GetEnvironmentVariable("NUGET_PACKAGES")) ?? DefaultGlobalPackagesDirectory();
#pragma warning disable CA1308 // NuGet stores package folders in lower case.
        var target = Path.Combine(root, packageId.ToLowerInvariant(), version.ToLowerInvariant());
#pragma warning restore CA1308
        if (!Directory.Exists(target))
        {
            return false;
        }

        Directory.Delete(target, recursive: true);
        return true;
    }

    /// <summary>Resolves the NuGet global packages folder: the explicit directory, else a non-empty <c>NUGET_PACKAGES</c> value, else the folder NuGet reports for the consumer, else <c>~/.nuget/packages</c>.</summary>
    /// <param name="consumerDirectory">The consumer directory; relative results are made absolute against it.</param>
    /// <param name="explicitDirectory">An explicitly requested folder.</param>
    /// <param name="environmentValue">The <c>NUGET_PACKAGES</c> value.</param>
    /// <param name="queryDotnet">Asks dotnet for the folder from the consumer directory, returning the folder (see <see cref="ParseGlobalPackagesListing"/>), or <see langword="null"/> when it has none; <see langword="null"/> skips the query.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The absolute global packages folder.</returns>
    /// <exception cref="ArgumentException"><paramref name="consumerDirectory"/> is blank.</exception>
    public static async Task<string> ResolveGlobalPackagesDirectory(
        string consumerDirectory,
        string? explicitDirectory,
        string? environmentValue,
        Func<CancellationToken, Task<string?>>? queryDotnet,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerDirectory);

        var folder = FirstNonBlank(explicitDirectory, environmentValue);
        if (folder is null && queryDotnet is not null)
        {
            folder = FirstNonBlank(await queryDotnet(ct).ConfigureAwait(false));
        }

        return Path.GetFullPath(folder ?? DefaultGlobalPackagesDirectory(), Path.GetFullPath(consumerDirectory));
    }

    /// <summary>Extracts the folder from <c>dotnet nuget locals global-packages --list</c> output.</summary>
    /// <param name="output">The command output.</param>
    /// <returns>The folder, or <see langword="null"/> when none is reported.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static string? ParseGlobalPackagesListing(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        const string Prefix = "global-packages:";
        return output
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            .Select(line => line[Prefix.Length..].Trim())
            .FirstOrDefault(folder => folder.Length > 0);
    }

    private static string? FirstNonBlank(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string DefaultGlobalPackagesDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    private static List<string> ResolveRequested(IReadOnlyCollection<string> requested, Dictionary<string, string> projectsByName)
    {
        var roots = new List<string>();
        foreach (var name in requested)
        {
            if (!projectsByName.TryGetValue(name, out var path))
            {
                throw new ArgumentException($"Unknown SharedKernel project: {name}");
            }

            roots.Add(Path.GetFileNameWithoutExtension(path));
        }

        return roots;
    }

    private static void Visit(string name, Dictionary<string, string> projectsByName, HashSet<string> visited, List<string> ordered)
    {
        if (!visited.Add(name))
        {
            return;
        }

        foreach (var dependency in ReferencedProjects(projectsByName[name], projectsByName))
        {
            Visit(dependency, projectsByName, visited, ordered);
        }

        ordered.Add(name);
    }

    private static IEnumerable<string> ReferencedProjects(string projectPath, Dictionary<string, string> projectsByName)
    {
        var directory = Path.GetDirectoryName(projectPath)!;
        foreach (var include in XDocument.Load(projectPath).Descendants("ProjectReference").Select(reference => (string?)reference.Attribute("Include")))
        {
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            var referenced = Path.GetFullPath(Path.Combine(directory, include.Replace('\\', '/')));
            var name = Path.GetFileNameWithoutExtension(referenced);
            if (projectsByName.TryGetValue(name, out var known) && string.Equals(Path.GetFullPath(known), referenced, StringComparison.OrdinalIgnoreCase))
            {
                yield return name;
            }
        }
    }

    private static string? ReadPackageId(string projectPath) =>
        XDocument.Load(projectPath).Descendants("PackageId").Select(element => element.Value.Trim()).FirstOrDefault(id => id.Length > 0);

    private static void EnsureSingleSegment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Contains('/', StringComparison.Ordinal) || value.Contains('\\', StringComparison.Ordinal) || value.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Not a single path segment: {value}");
        }
    }
}
