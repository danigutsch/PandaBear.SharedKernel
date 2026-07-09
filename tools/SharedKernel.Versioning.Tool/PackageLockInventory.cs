using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SharedKernel.Versioning.Tool;

internal static class PackageLockInventory
{
    public static ResolvedNuGetPackage[] Read(string repositoryRoot)
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

        var packageLockFiles = Directory.EnumerateFiles(sourceRoot, "packages.lock.json", SearchOption.AllDirectories)
            .Where(IsMaintainedLockFile)
            .ToArray();
        if (packageLockFiles.Length == 0)
        {
            throw new ArgumentException($"No packages.lock.json files found under source directory: {sourceRoot}");
        }

        var packages = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var lockFile in packageLockFiles)
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
                    if (!package.Value.TryGetProperty("resolved", out var resolved))
                    {
                        continue;
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
            }
        }

        return packages
            .Select(entry =>
            {
                var separator = entry.Key.LastIndexOf('@');
                return new ResolvedNuGetPackage(entry.Key[..separator], entry.Key[(separator + 1)..], entry.Value.ToArray());
            })
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string WriteAttributionJson(IReadOnlyCollection<ResolvedNuGetPackage> packages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"source\": \"packages.lock.json\",");
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

    public static string WriteNoticesMarkdown(IReadOnlyCollection<ResolvedNuGetPackage> packages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Third-party notices");
        builder.AppendLine();
        builder.AppendLine("Generated from resolved `packages.lock.json` files. License expressions are `NOASSERTION` until reviewed against NuGet and dependency-review metadata.");
        builder.AppendLine();
        builder.AppendLine("| Package | Version | License evidence | Review required |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var package in packages)
        {
            builder.Append("| `").Append(package.Id).Append("` | `").Append(package.Version).Append("` | `NOASSERTION` | yes |").AppendLine();
        }

        return builder.ToString();
    }

    public static string WriteSpdxJson(PrepareReleaseOptions options, IReadOnlyCollection<ResolvedNuGetPackage> packages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"spdxVersion\": \"SPDX-2.3\",");
        builder.AppendLine("  \"dataLicense\": \"CC0-1.0\",");
        builder.Append("  \"SPDXID\": ").Append(Escape("SPDXRef-DOCUMENT")).AppendLine(",");
        builder.Append("  \"name\": ").Append(Escape("ViajantesTurismo " + options.Version)).AppendLine(",");
        builder.Append("  \"documentNamespace\": ").Append(Escape("https://github.com/danigutsch/ViajantesTurismo/sbom/" + options.Version)).AppendLine(",");
        builder.AppendLine("  \"creationInfo\": {");
        builder.AppendLine("    \"creators\": [\"Tool: SharedKernel.Versioning.Tool\"],");
        builder.Append("    \"created\": ").Append(Escape(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))).AppendLine();
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

    private static bool IsMaintainedLockFile(string path) =>
        !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        && !path.Contains(Path.DirectorySeparatorChar + ".worktrees" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        && !path.Contains(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static JsonDocument ReadLockFile(string lockFile)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(lockFile));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid packages.lock.json: {lockFile}", ex);
        }
    }

    private static string SanitizeSpdxId(string value) => new(value.Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());

    private static string Escape(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
