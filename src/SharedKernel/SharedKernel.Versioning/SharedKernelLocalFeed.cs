using System.IO.Compression;
using System.Xml.Linq;

namespace SharedKernel.Versioning;

/// <summary>
/// Builds and validates local package-feed assets for SharedKernel package sets.
/// </summary>
public static class SharedKernelLocalFeed
{
    /// <summary>
    /// Reads package IDs from NuGet package files and validates their SharedKernel dependency versions.
    /// </summary>
    /// <param name="packages">Package file paths.</param>
    /// <param name="version">Expected package version.</param>
    /// <returns>Sorted package IDs.</returns>
    /// <exception cref="ArgumentException">Thrown when package metadata is missing or invalid.</exception>
    public static string[] ReadPackageIds(string[] packages, string version)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var packageIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var package in packages)
        {
            using var archive = ZipFile.OpenRead(package);
            var nuspecEntries = archive.Entries.Where(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).ToArray();
            if (nuspecEntries.Length != 1)
            {
                throw new ArgumentException($"{package}: expected one .nuspec, found {nuspecEntries.Length}");
            }

            using var stream = nuspecEntries[0].Open();
            var document = XDocument.Load(stream);
            var packageId = RequiredElement(document, "id", package);
            var packageVersion = RequiredElement(document, "version", package);
            if (packageVersion != version)
            {
                throw new ArgumentException($"{package}: expected version {version}, found {packageVersion}");
            }

            foreach (var dependency in document.Descendants().Where(element => element.Name.LocalName == "dependency"))
            {
                var dependencyId = dependency.Attribute("id")?.Value ?? string.Empty;
                var dependencyVersion = dependency.Attribute("version")?.Value ?? string.Empty;
                if (dependencyId.StartsWith("SharedKernel.", StringComparison.Ordinal) && dependencyVersion != version)
                {
                    throw new ArgumentException($"{package}: expected {dependencyId} dependency version {version}, found {dependencyVersion}");
                }
            }

            if (!seen.Add(packageId))
            {
                throw new ArgumentException("duplicate packages: " + packageId);
            }

            packageIds.Add(packageId);
        }

        return packageIds.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Determines whether a package version exists in a scratch package cache.
    /// </summary>
    /// <param name="packageCache">The package cache directory.</param>
    /// <param name="packageId">The package ID.</param>
    /// <param name="version">The package version.</param>
    /// <returns><see langword="true" /> when the package version was restored.</returns>
    public static bool PackageWasRestored(string packageCache, string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageCache);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (!Directory.Exists(packageCache))
        {
            return false;
        }

        return Directory.EnumerateDirectories(packageCache)
            .Where(directory => string.Equals(Path.GetFileName(directory), packageId, StringComparison.OrdinalIgnoreCase))
            .Any(directory => Directory.EnumerateDirectories(directory)
                .Any(versionDirectory => string.Equals(Path.GetFileName(versionDirectory), version, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Writes a scratch restore project for local-feed validation.
    /// </summary>
    /// <param name="restoreDirectory">The scratch restore directory.</param>
    /// <param name="packageIds">The package IDs to reference.</param>
    /// <param name="version">The package version.</param>
    /// <returns>A task that completes when the project file is written.</returns>
    public static async Task WriteRestoreProject(string restoreDirectory, string[] packageIds, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(restoreDirectory);
        ArgumentNullException.ThrowIfNull(packageIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        await File.WriteAllTextAsync(Path.Combine(restoreDirectory, "Directory.Build.props"), "<Project />" + Environment.NewLine).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(restoreDirectory, "Directory.Packages.props"), "<Project />" + Environment.NewLine).ConfigureAwait(false);

        var references = string.Join(
            Environment.NewLine,
            packageIds.Select(packageId => "    <PackageReference Include=\"" + packageId + "\" Version=\"" + version + "\" />"));
        var project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
            """ + Environment.NewLine + references + Environment.NewLine + """
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(Path.Combine(restoreDirectory, "SharedKernel.LocalFeedRestore.csproj"), project).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a NuGet configuration that maps SharedKernel packages to a local package directory.
    /// </summary>
    /// <param name="restoreDirectory">The scratch restore directory.</param>
    /// <param name="packageDirectory">The local package directory.</param>
    /// <param name="packageIds">The package IDs restored from the local feed.</param>
    /// <param name="nugetSourceUrl">The fallback NuGet source URL.</param>
    /// <returns>A task that completes when the configuration file is written.</returns>
    public static async Task WriteNuGetConfig(string restoreDirectory, string packageDirectory, string[] packageIds, Uri nugetSourceUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(restoreDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentNullException.ThrowIfNull(packageIds);
        ArgumentNullException.ThrowIfNull(nugetSourceUrl);
        if (!nugetSourceUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("NuGet source URL must be absolute.", nameof(nugetSourceUrl));
        }

        var configuration = new XElement("configuration",
            new XElement("packageSources",
                new XElement("clear"),
                new XElement("add",
                    new XAttribute("key", "sharedkernel-local"),
                    new XAttribute("value", Path.GetFullPath(packageDirectory))),
                new XElement("add",
                    new XAttribute("key", "nuget.org"),
                    new XAttribute("value", nugetSourceUrl.AbsoluteUri),
                    new XAttribute("protocolVersion", "3"))),
            new XElement("packageSourceMapping",
                new XElement("packageSource",
                    new XAttribute("key", "sharedkernel-local"),
                    packageIds.Select(packageId => new XElement("package", new XAttribute("pattern", packageId)))),
                new XElement("packageSource",
                    new XAttribute("key", "nuget.org"),
                    new XElement("package", new XAttribute("pattern", "*")))));

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), configuration);
        await File.WriteAllTextAsync(Path.Combine(restoreDirectory, "NuGet.config"), document + Environment.NewLine).ConfigureAwait(false);
    }

    private static string RequiredElement(XDocument document, string elementName, string package)
    {
        var value = document.Descendants().FirstOrDefault(element => element.Name.LocalName == elementName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{package}: missing {elementName}");
        }

        return value.Trim();
    }

}
