namespace SharedKernel.Versioning.Tool;

internal static class SharedKernelPackCommand
{
    private static readonly string NuGetSourceUrl = string.Concat("https://api.nuget.org", "/v3/index.json");

    public static async Task<string> Run(PackSharedKernelOptions options, TextWriter output)
    {
        var packageDirectory = Path.Combine(ResolveOutputRoot(options), options.Version);
        if (Directory.Exists(packageDirectory) && PackageVersionExists(packageDirectory, options.Version))
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
                CreatePackArguments(project, options, packageDirectory),
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

    private static bool PackageVersionExists(string packageDirectory, string version) =>
        Directory.EnumerateFiles(packageDirectory, "SharedKernel.*")
            .Select(Path.GetFileName)
            .Any(fileName => fileName is not null && IsPackageVersionFile(fileName, version));

    private static bool IsPackageVersionFile(string fileName, string version) =>
        fileName.EndsWith("." + version + ".nupkg", StringComparison.Ordinal)
        || fileName.EndsWith("." + version + ".snupkg", StringComparison.Ordinal)
        || fileName.EndsWith("." + version + ".symbols.nupkg", StringComparison.Ordinal);

    private static string ResolveOutputRoot(PackSharedKernelOptions options) =>
        Path.IsPathRooted(options.OutputRoot)
            ? options.OutputRoot
            : Path.Combine(options.RepoRoot, options.OutputRoot);

    private static string[] CreatePackArguments(string project, PackSharedKernelOptions options, string packageDirectory)
    {
        var arguments = new List<string>
        {
            "pack",
            project,
            "-c",
            "Release",
            "-p:ComputedSemVer=" + options.Version,
            "-o",
            packageDirectory,
        };

        AddOptionalProperty(arguments, "ComputedAssemblyVersion", options.AssemblyVersion);
        AddOptionalProperty(arguments, "ComputedFileVersion", options.FileVersion);
        AddOptionalProperty(arguments, "ComputedInformationalVersion", options.InformationalVersion);
        AddOptionalProperty(arguments, "EnablePackageValidation", Environment.GetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.EnablePackageValidation));
        AddOptionalProperty(arguments, "PackageValidationBaselineVersion", Environment.GetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.BaselineVersion));
        return [.. arguments];
    }

    private static void AddOptionalProperty(List<string> arguments, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments.Add("-p:" + propertyName + "=" + value);
        }
    }

}
