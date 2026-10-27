using System.Xml.Linq;

namespace PandaBear.Versioning.Tool;

/// <summary>Packs all SharedKernel projects into a local package directory.</summary>
internal static class SharedKernelPackCommand
{
    private static readonly Uri NuGetSourceUrl = new(string.Concat("https://api.nuget.org", "/v3/index.json"));

    /// <summary>Packs the projects with the requested versions, optionally verifies restore from the resulting feed, and returns the package directory.</summary>
    public static async Task<string> Run(PackSharedKernelOptions options, TextWriter output, CancellationToken ct)
    {
        var packageOptions = new SharedKernelPackageOptions(
            options.Version,
            options.OutputRoot,
            options.RepoRoot,
            options.AssemblyVersion,
            options.FileVersion,
            options.InformationalVersion);
        var packageDirectory = SharedKernelPackagePlanner.ResolvePackageDirectory(packageOptions);
        SharedKernelPackagePlanner.EnsurePackageVersionDoesNotExist(packageDirectory, options.Version);

        Directory.CreateDirectory(packageDirectory);
        var projects = SharedKernelPackagePlanner.FindProjects(options.RepoRoot);
        if (projects.Length == 0)
        {
            throw new ArgumentException("No SharedKernel projects found.");
        }

        var solution = Path.Combine(packageDirectory, ".pack.slnx");
        new XDocument(new XElement("Solution", projects.Select(project =>
            new XElement("Project", new XAttribute("Path", Path.GetFullPath(project)))))).Save(solution);
        var baselineVersion = options.PackageValidationBaselineVersion;
        await CommandRunner.Run(
            "dotnet",
            SharedKernelPackagePlanner.CreatePackArguments(
                solution, packageOptions, packageDirectory,
                string.IsNullOrWhiteSpace(baselineVersion) ? null : "true",
                baselineVersion),
            options.RepoRoot,
            ct).ConfigureAwait(false);

        if (options.VerifyRestore)
        {
            await VerifyLocalFeed(packageDirectory, options.Version, ct).ConfigureAwait(false);
        }

        await output.WriteLineAsync("SharedKernel packages: " + packageDirectory).ConfigureAwait(false);
        return packageDirectory;
    }

    private static async Task VerifyLocalFeed(string packageDirectory, string version, CancellationToken ct)
    {
        var packages = Directory.GetFiles(packageDirectory, "PandaBear.SharedKernel.*.nupkg")
            .Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (packages.Length == 0)
        {
            throw new ArgumentException($"No SharedKernel packages found in {packageDirectory}");
        }

        _ = SharedKernelLocalFeed.ReadPackageIds(packages, version);
        var tools = packages.Where(ToolPackageCheck.IsToolPackage).ToArray();
        var packageIds = SharedKernelLocalFeed.ReadPackageIds(packages.Except(tools).ToArray(), version);
        var restoreDirectory = Path.Combine(packageDirectory, ".restore-check");
        if (Directory.Exists(restoreDirectory))
        {
            Directory.Delete(restoreDirectory, recursive: true);
        }

        Directory.CreateDirectory(restoreDirectory);
        await SharedKernelLocalFeed.WriteRestoreProject(restoreDirectory, packageIds, version).ConfigureAwait(false);
        await SharedKernelLocalFeed.WriteNuGetConfig(restoreDirectory, packageDirectory, packageIds, NuGetSourceUrl).ConfigureAwait(false);
        var packageCache = Path.Combine(restoreDirectory, "packages");
        await CommandRunner.Run(
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
            ],
            null,
            ct).ConfigureAwait(false);

        foreach (var tool in tools)
        {
            var id = SharedKernelLocalFeed.ReadPackageIds([tool], version).Single();
            await CommandRunner.Run("dotnet", ["tool", "install", id, "--version", version,
                "--tool-path", Path.Combine(restoreDirectory, "tools", id), "--configfile",
                Path.Combine(restoreDirectory, "NuGet.config"), "--no-cache"], restoreDirectory, ct).ConfigureAwait(false);
        }

        var missing = packageIds.Where(packageId => !SharedKernelLocalFeed.PackageWasRestored(packageCache, packageId, version)).ToArray();
        if (missing.Length > 0)
        {
            throw new ArgumentException("packages not restored from local feed: " + string.Join(", ", missing));
        }
    }

}
