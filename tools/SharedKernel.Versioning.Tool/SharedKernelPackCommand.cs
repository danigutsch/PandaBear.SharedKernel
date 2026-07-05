using System.IO.Compression;
using System.Xml.Linq;

namespace SharedKernel.Versioning.Tool;

internal static class SharedKernelPackCommand
{
    private static readonly string NuGetSourceUrl = string.Concat("https://api.nuget.org", "/v3/index.json");

    public static async Task<string> Run(PackSharedKernelOptions options, TextWriter output)
    {
        var packageDirectory = Path.Combine(options.OutputRoot, options.Version);
        if (Directory.Exists(packageDirectory) && Directory.GetFiles(packageDirectory, "SharedKernel.*." + options.Version + ".nupkg").Length > 0)
        {
            throw new ArgumentException($"Package version already exists in {packageDirectory}: {options.Version}");
        }

        Directory.CreateDirectory(packageDirectory);
        var projects = Directory.GetFiles(Path.Combine(options.RepoRoot, "src", "SharedKernel"), "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (projects.Length == 0)
        {
            throw new ArgumentException("No SharedKernel projects found.");
        }

        foreach (var project in projects)
        {
            CommandRunner.Run(
                "dotnet",
                ["pack", project, "-c", "Release", "-p:ComputedSemVer=" + options.Version, "-o", packageDirectory],
                options.RepoRoot);
        }

        if (options.VerifyRestore)
        {
            await VerifyLocalFeed(packageDirectory, options.Version).ConfigureAwait(false);
        }

        await output.WriteLineAsync("SharedKernel packages: " + packageDirectory).ConfigureAwait(false);
        return packageDirectory;
    }

    private static async Task VerifyLocalFeed(string packageDirectory, string version)
    {
        var packages = Directory.GetFiles(packageDirectory, "SharedKernel.*.nupkg")
            .Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (packages.Length == 0)
        {
            throw new ArgumentException($"no SharedKernel packages found in {packageDirectory}");
        }

        var packageIds = ReadPackageIds(packages, version);
        var restoreDirectory = Path.Combine(packageDirectory, ".restore-check");
        if (Directory.Exists(restoreDirectory))
        {
            Directory.Delete(restoreDirectory, recursive: true);
        }

        Directory.CreateDirectory(restoreDirectory);
        await WriteRestoreProject(restoreDirectory, packageIds, version).ConfigureAwait(false);
        await WriteNuGetConfig(restoreDirectory, packageDirectory, packageIds).ConfigureAwait(false);
        var packageCache = Path.Combine(restoreDirectory, "packages");
        CommandRunner.Run(
            "dotnet",
            [
                "restore",
                Path.Combine(restoreDirectory, "SharedKernel.LocalFeedRestore.csproj"),
                "--configfile",
                Path.Combine(restoreDirectory, "NuGet.config"),
                "--packages",
                packageCache,
                "--no-cache",
                "-p:RestorePackagesWithLockFile=false",
            ]);

        var missing = packageIds.Where(packageId => !PackageWasRestored(packageCache, packageId, version)).ToArray();
        if (missing.Length > 0)
        {
            throw new ArgumentException("packages not restored from local feed: " + string.Join(", ", missing));
        }
    }

    private static bool PackageWasRestored(string packageCache, string packageId, string version)
    {
        if (!Directory.Exists(packageCache))
        {
            return false;
        }

        return Directory.EnumerateDirectories(packageCache)
            .Where(directory => string.Equals(Path.GetFileName(directory), packageId, StringComparison.OrdinalIgnoreCase))
            .Any(directory => Directory.EnumerateDirectories(directory)
                .Any(versionDirectory => string.Equals(Path.GetFileName(versionDirectory), version, StringComparison.OrdinalIgnoreCase)));
    }

    private static string[] ReadPackageIds(string[] packages, string version)
    {
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

    private static string RequiredElement(XDocument document, string elementName, string package)
    {
        var value = document.Descendants().FirstOrDefault(element => element.Name.LocalName == elementName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{package}: missing {elementName}");
        }

        return value.Trim();
    }

    private static async Task WriteRestoreProject(string restoreDirectory, string[] packageIds, string version)
    {
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

    private static async Task WriteNuGetConfig(string restoreDirectory, string packageDirectory, string[] packageIds)
    {
        var configuration = new XElement("configuration",
            new XElement("packageSources",
                new XElement("clear"),
                new XElement("add",
                    new XAttribute("key", "sharedkernel-local"),
                    new XAttribute("value", Path.GetFullPath(packageDirectory))),
                new XElement("add",
                    new XAttribute("key", "nuget.org"),
                    new XAttribute("value", NuGetSourceUrl),
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
}
