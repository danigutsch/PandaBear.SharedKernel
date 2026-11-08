using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PandaBear.Versioning;

/// <summary>
/// Reads lock-file package inventories and writes release attribution artifacts.
/// </summary>
public static class PackageLockInventory
{
    private const string SourceDateEpochVariableName = "SOURCE_DATE_EPOCH";

    /// <summary>
    /// Reads resolved packages from the maintained source projects under a repository root: the
    /// <c>packages.lock.json</c> next to each project, or, for a project that keeps no lock file, the packages that its
    /// restored <c>obj/project.assets.json</c> resolves.
    /// </summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <returns>The resolved packages.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="repositoryRoot"/> is <see langword="null"/>, empty, or white-space; when a source
    /// project has neither a lock file nor restored assets; or when the restored assets of a project without a lock file
    /// resolve a package version that no committed lock file resolves.
    /// </exception>
    public static ResolvedNuGetPackage[] Read(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var (packageLockFiles, unlockedProjects) = GetSources(repositoryRoot);
        var packages = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var lockFile in packageLockFiles)
        {
            AddLockFilePackages(repositoryRoot, lockFile, packages);
        }

        var assetsPackages = new List<(string Project, string Package)>();
        foreach (var project in unlockedProjects)
        {
            AddAssetsPackages(repositoryRoot, project, packages, assetsPackages);
        }

        VerifyAssetsPackagesAreLocked(repositoryRoot, assetsPackages);
        return ToResolvedPackages(packages);
    }

    /// <summary>
    /// Verifies the lock coverage drift guard without reading a package directory: the restored <c>obj/project.assets.json</c>
    /// of every source project that keeps no <c>packages.lock.json</c> may resolve only package versions that some committed
    /// lock file resolves, because the dependency snapshot reads lock files alone.
    /// </summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <returns>The number of lock-free source projects that were checked, which is zero when every source project keeps a lock file.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="repositoryRoot"/> is <see langword="null"/>, empty, or white-space; when a lock-free source
    /// project has no restored assets, so the check never passes vacuously; or when the restored assets of a lock-free project
    /// resolve a package version that no committed lock file resolves.
    /// </exception>
    public static int VerifyLockCoverage(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var (_, unlockedProjects) = GetSources(repositoryRoot);
        var assetsPackages = new List<(string Project, string Package)>();
        foreach (var project in unlockedProjects)
        {
            AddAssetsPackages(repositoryRoot, project, packages: null, assetsPackages);
        }

        VerifyAssetsPackagesAreLocked(repositoryRoot, assetsPackages);
        return unlockedProjects.Length;
    }

    /// <summary>
    /// Writes third-party attribution JSON with review-required license placeholders.
    /// </summary>
    /// <param name="packages">The resolved packages.</param>
    /// <returns>The attribution JSON.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="packages"/> is <see langword="null"/>.</exception>
    public static string WriteAttributionJson(IReadOnlyCollection<ResolvedNuGetPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"source\": \"packages.lock.json; obj/project.assets.json for projects without a lock file\",");
        builder.AppendLine("  \"licenseEvidence\": \"NOASSERTION; review NuGet and dependency-review metadata before release\",");
        builder.AppendLine("  \"packages\": [");
        var index = 0;
        foreach (var package in packages)
        {
            builder.AppendLine("    {");
            builder.Append("      \"id\": ").Append(Escape(package.Id)).AppendLine(",");
            builder.Append("      \"version\": ").Append(Escape(package.Version)).AppendLine(",");
            builder.AppendLine("      \"licenseExpression\": \"NOASSERTION\",");
            builder.AppendLine("      \"requiresLicenseReview\": true,");
            builder.Append("      \"lockFiles\": [").Append(string.Join(", ", package.LockFiles.Select(Escape))).AppendLine("]");
            builder.Append(++index == packages.Count ? "    }" : "    },");
            builder.AppendLine();
        }

        builder.AppendLine("  ]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    /// Writes third-party notices markdown with review-required license placeholders.
    /// </summary>
    /// <param name="packages">The resolved packages.</param>
    /// <returns>The notices markdown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="packages"/> is <see langword="null"/>.</exception>
    public static string WriteNoticesMarkdown(IReadOnlyCollection<ResolvedNuGetPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var builder = new StringBuilder();
        builder.AppendLine("# Third-party notices");
        builder.AppendLine();
        builder.AppendLine("Generated from resolved `packages.lock.json` files and, for projects without a lock file, their restored `obj/project.assets.json`. License expressions are `NOASSERTION` until reviewed against NuGet and dependency-review metadata.");
        builder.AppendLine();
        builder.AppendLine("| Package | Version | License evidence | Review required |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var package in packages)
        {
            builder.Append("| `").Append(package.Id).Append("` | `").Append(package.Version).Append("` | `NOASSERTION` | yes |").AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Writes an SPDX JSON document for resolved packages.
    /// </summary>
    /// <param name="options">The release-preparation options.</param>
    /// <param name="packages">The resolved packages.</param>
    /// <returns>The SPDX JSON document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="packages"/> is <see langword="null"/>.</exception>
    public static string WriteSpdxJson(ReleasePreparationOptions options, IReadOnlyCollection<ResolvedNuGetPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(packages);

        var created = GetCreatedTimestamp().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var documentNamespace = ReadRepositoryUrl(options.RepositoryRoot).TrimEnd('/') + "/sbom/" + options.Version;
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"spdxVersion\": \"SPDX-2.3\",");
        builder.AppendLine("  \"dataLicense\": \"CC0-1.0\",");
        builder.Append("  \"SPDXID\": ").Append(Escape("SPDXRef-DOCUMENT")).AppendLine(",");
        builder.Append("  \"name\": ").Append(Escape("SharedKernel " + options.Version)).AppendLine(",");
        builder.Append("  \"documentNamespace\": ").Append(Escape(documentNamespace)).AppendLine(",");
        builder.AppendLine("  \"creationInfo\": {");
        builder.AppendLine("    \"creators\": [\"Tool: PandaBear.Versioning.Tool\"],");
        builder.Append("    \"created\": ").Append(Escape(created)).AppendLine();
        builder.AppendLine("  },");
        builder.AppendLine("  \"packages\": [");
        var index = 0;
        foreach (var package in packages)
        {
            builder.AppendLine("    {");
            builder.Append("      \"name\": ").Append(Escape(package.Id)).AppendLine(",");
            builder.Append("      \"SPDXID\": ").Append(Escape("SPDXRef-Package-" + SanitizeSpdxId(package.Id) + "-" + SanitizeSpdxId(package.Version))).AppendLine(",");
            builder.Append("      \"versionInfo\": ").Append(Escape(package.Version)).AppendLine(",");
            builder.AppendLine("      \"downloadLocation\": \"NOASSERTION\",");
            builder.AppendLine("      \"filesAnalyzed\": false,");
            builder.AppendLine("      \"licenseConcluded\": \"NOASSERTION\",");
            builder.AppendLine("      \"licenseDeclared\": \"NOASSERTION\",");
            builder.AppendLine("      \"copyrightText\": \"NOASSERTION\"");
            builder.Append(++index == packages.Count ? "    }" : "    },");
            builder.AppendLine();
        }

        builder.AppendLine("  ]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static readonly HashSet<string> GeneratedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", ".worktrees", "artifacts", "node_modules",
    };

    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

    /// <summary>Enumerates the files below <paramref name="directory"/> that a name filter accepts, without entering generated or tool directories.</summary>
    private static IEnumerable<string> EnumerateMaintainedFiles(string directory, Func<string, bool> accepts)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (accepts(Path.GetFileName(file)))
            {
                yield return file;
            }
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (GeneratedDirectories.Contains(Path.GetFileName(child)))
            {
                continue;
            }

            foreach (var file in EnumerateMaintainedFiles(child, accepts))
            {
                yield return file;
            }
        }
    }

    private static bool IsLockFileName(string name) => string.Equals(name, "packages.lock.json", StringComparison.Ordinal);

    private static bool IsProjectFileName(string name) =>
        ProjectExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    private static string ToRelative(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

    private static (string[] LockFiles, string[] UnlockedProjects) GetSources(string repositoryRoot)
    {
        if (!Directory.Exists(repositoryRoot))
        {
            throw new ArgumentException($"Repository root does not exist: {repositoryRoot}");
        }

        var sourceRoot = Path.Combine(repositoryRoot, "src");
        if (!Directory.Exists(sourceRoot))
        {
            throw new ArgumentException($"Source directory does not exist: {sourceRoot}");
        }

        var packageLockFiles = EnumerateMaintainedFiles(sourceRoot, IsLockFileName).ToArray();
        var unlockedProjects = EnumerateMaintainedFiles(sourceRoot, IsProjectFileName)
            .Where(project => !File.Exists(Path.Combine(Path.GetDirectoryName(project)!, "packages.lock.json")))
            .ToArray();
        if (packageLockFiles.Length == 0 && unlockedProjects.Length == 0)
        {
            throw new ArgumentException($"No packages.lock.json files found under source directory: {sourceRoot}");
        }

        return (packageLockFiles, unlockedProjects);
    }

    private static void AddAssetsPackages(
        string repositoryRoot,
        string project,
        Dictionary<string, SortedSet<string>>? packages,
        List<(string Project, string Package)> assetsPackages)
    {
        var assetsFile = Path.Combine(Path.GetDirectoryName(project)!, "obj", "project.assets.json");
        if (!File.Exists(assetsFile))
        {
            throw new ArgumentException(
                $"{ToRelative(repositoryRoot, project)} has no packages.lock.json and no restored obj/project.assets.json; run dotnet restore for the solution before reading the package inventory.");
        }

        using var document = ReadJson(assetsFile, "project.assets.json");
        if (!document.RootElement.TryGetProperty("targets", out var targets))
        {
            throw new ArgumentException($"Unexpected project.assets.json schema: {assetsFile}. Missing top-level targets section.");
        }

        var relativeAssets = ToRelative(repositoryRoot, assetsFile);
        foreach (var target in targets.EnumerateObject())
        {
            foreach (var entry in target.Value.EnumerateObject())
            {
                // Entries are "Id/Version"; project references have the type "project".
                if (!entry.Value.TryGetProperty("type", out var type) || type.GetString() != "package")
                {
                    continue;
                }

                var separator = entry.Name.LastIndexOf('/');
                if (separator <= 0 || separator == entry.Name.Length - 1)
                {
                    throw new ArgumentException($"Package entry {entry.Name} in {assetsFile} has no resolved version.");
                }

                var key = entry.Name[..separator] + "@" + entry.Name[(separator + 1)..];
                if (packages is not null)
                {
                    if (!packages.TryGetValue(key, out var sources))
                    {
                        sources = new SortedSet<string>(StringComparer.Ordinal);
                        packages.Add(key, sources);
                    }

                    sources.Add(relativeAssets);
                }

                assetsPackages.Add((ToRelative(repositoryRoot, project), key));
            }
        }
    }

    // A package that only a lock-free project resolves is invisible to the committed dependency snapshot, which is built
    // from lock files alone, so every such package version must also appear in some committed lock file.
    private static void VerifyAssetsPackagesAreLocked(string repositoryRoot, List<(string Project, string Package)> assetsPackages)
    {
        if (assetsPackages.Count == 0)
        {
            return;
        }

        var locked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lockFile in EnumerateMaintainedFiles(repositoryRoot, IsLockFileName))
        {
            var packages = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            AddLockFilePackages(repositoryRoot, lockFile, packages);
            locked.UnionWith(packages.Keys);
        }

        var uncovered = assetsPackages
            .Where(entry => !locked.Contains(entry.Package))
            .Distinct()
            .OrderBy(entry => entry.Project, StringComparer.Ordinal)
            .ThenBy(entry => entry.Package, StringComparer.OrdinalIgnoreCase)
            .Select(entry => $"{entry.Project}: {entry.Package}")
            .ToArray();
        if (uncovered.Length > 0)
        {
            throw new ArgumentException(
                "Restored assets of projects without a packages.lock.json resolve packages that no committed lock file resolves, so the dependency snapshot would omit them:"
                + Environment.NewLine + string.Join(Environment.NewLine, uncovered)
                + Environment.NewLine + "Remove each project from lockExemptProjects in .config/sharedkernel-repo.json and its project file's RestorePackagesWithLockFile=false, then restore to create its packages.lock.json.");
        }
    }

    private static void AddLockFilePackages(string repositoryRoot, string lockFile, Dictionary<string, SortedSet<string>> packages)
    {
        using var document = ReadLockFile(lockFile);
        if (!document.RootElement.TryGetProperty("dependencies", out var frameworks))
        {
            throw new ArgumentException($"Unexpected packages.lock.json schema: {lockFile}. Missing top-level dependencies section.");
        }

        foreach (var framework in frameworks.EnumerateObject())
        {
            foreach (var package in framework.Value.EnumerateObject())
            {
                AddPackage(packages, package, repositoryRoot, lockFile);
            }
        }
    }

    private static void AddPackage(Dictionary<string, SortedSet<string>> packages, JsonProperty package, string repositoryRoot, string lockFile)
    {
        if (!package.Value.TryGetProperty("resolved", out var resolved))
        {
            return;
        }

        var version = resolved.GetString();
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException($"Package {package.Name} in {lockFile} has no resolved version.");
        }

        var key = package.Name + "@" + version;
        if (!packages.TryGetValue(key, out var lockFiles))
        {
            lockFiles = new SortedSet<string>(StringComparer.Ordinal);
            packages.Add(key, lockFiles);
        }

        lockFiles.Add(Path.GetRelativePath(repositoryRoot, lockFile).Replace('\\', '/'));
    }

    private static ResolvedNuGetPackage[] ToResolvedPackages(Dictionary<string, SortedSet<string>> packages) =>
        packages
            .Select(entry =>
            {
                var separator = entry.Key.LastIndexOf('@');
                return new ResolvedNuGetPackage(entry.Key[..separator], entry.Key[(separator + 1)..], entry.Value.ToArray());
            })
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static JsonDocument ReadLockFile(string lockFile) => ReadJson(lockFile, "packages.lock.json");

    private static JsonDocument ReadJson(string path, string description)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonDocument.Parse(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new ArgumentException($"Invalid {description}: {path}", ex);
        }
    }

    private static DateTimeOffset GetCreatedTimestamp()
    {
        var sourceDateEpoch = Environment.GetEnvironmentVariable(SourceDateEpochVariableName);
        if (string.IsNullOrWhiteSpace(sourceDateEpoch))
        {
            return TimeProvider.System.GetUtcNow();
        }

        if (!long.TryParse(sourceDateEpoch, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            throw new ArgumentException($"{SourceDateEpochVariableName} must be a Unix timestamp in seconds.");
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new ArgumentException($"{SourceDateEpochVariableName} is outside the supported Unix timestamp range.", ex);
        }
    }

    private static string ReadRepositoryUrl(string repositoryRoot)
    {
        var rootProps = Path.Combine(repositoryRoot, "Directory.Build.props");
        if (!File.Exists(rootProps))
        {
            throw new ArgumentException($"Directory.Build.props does not exist: {rootProps}");
        }

        return ProjectPropertyReader.Read(rootProps, "RepositoryUrl")
            ?? throw new ArgumentException("Directory.Build.props missing RepositoryUrl.");
    }

    private static string SanitizeSpdxId(string value) => new(value.Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());

    private static string Escape(string value) => "\"" + value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";
}
