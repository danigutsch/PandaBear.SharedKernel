namespace SharedKernel.Versioning.Tool;

internal static class SharedKernelPackCommand
{
    private static readonly Uri NuGetSourceUrl = new(string.Concat("https://api.nuget.org", "/v3/index.json"));

    public static async Task<string> Run(PackSharedKernelOptions options, TextWriter output)
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

        foreach (var project in projects)
        {
            CommandRunner.Run(
                "dotnet",
                SharedKernelPackagePlanner.CreatePackArguments(
                    project,
                    packageOptions,
                    packageDirectory,
                    Environment.GetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.EnablePackageValidation),
                    Environment.GetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.BaselineVersion)),
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
            throw new ArgumentException($"No SharedKernel packages found in {packageDirectory}");
        }

        var packageIds = SharedKernelLocalFeed.ReadPackageIds(packages, version);
        var restoreDirectory = Path.Combine(packageDirectory, ".restore-check");
        if (Directory.Exists(restoreDirectory))
        {
            Directory.Delete(restoreDirectory, recursive: true);
        }

        Directory.CreateDirectory(restoreDirectory);
        await SharedKernelLocalFeed.WriteRestoreProject(restoreDirectory, packageIds, version).ConfigureAwait(false);
        await SharedKernelLocalFeed.WriteNuGetConfig(restoreDirectory, packageDirectory, packageIds, NuGetSourceUrl).ConfigureAwait(false);
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

        var missing = packageIds.Where(packageId => !SharedKernelLocalFeed.PackageWasRestored(packageCache, packageId, version)).ToArray();
        if (missing.Length > 0)
        {
            throw new ArgumentException("packages not restored from local feed: " + string.Join(", ", missing));
        }
    }

}
