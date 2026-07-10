namespace SharedKernel.Versioning;

/// <summary>
/// Plans SharedKernel package commands and validates package output folders.
/// </summary>
public static class SharedKernelPackagePlanner
{
    /// <summary>
    /// Resolves the package directory for a package version.
    /// </summary>
    /// <param name="options">The package options.</param>
    /// <returns>The package directory.</returns>
    public static string ResolvePackageDirectory(SharedKernelPackageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var outputRoot = Path.IsPathRooted(options.OutputRoot)
            ? options.OutputRoot
            : Path.Combine(options.RepoRoot, options.OutputRoot);
        return Path.Combine(outputRoot, options.Version);
    }

    /// <summary>
    /// Finds SharedKernel project files under a repository root.
    /// </summary>
    /// <param name="repoRoot">The repository root.</param>
    /// <returns>Sorted project file paths.</returns>
    public static string[] FindProjects(string repoRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var sharedKernelDirectory = Path.Combine(repoRoot, "src", "SharedKernel");
        if (!Directory.Exists(sharedKernelDirectory))
        {
            throw new ArgumentException($"SharedKernel directory does not exist: {sharedKernelDirectory}");
        }

        return Directory.GetFiles(sharedKernelDirectory, "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Ensures a package directory does not already contain artifacts for the target version.
    /// </summary>
    /// <param name="packageDirectory">The package directory.</param>
    /// <param name="version">The package version.</param>
    /// <exception cref="ArgumentException">Thrown when the version already exists.</exception>
    public static void EnsurePackageVersionDoesNotExist(string packageDirectory, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (Directory.Exists(packageDirectory) && PackageVersionExists(packageDirectory, version))
        {
            throw new ArgumentException($"Package version already exists in {packageDirectory}: {version}");
        }
    }

    /// <summary>
    /// Creates <c>dotnet pack</c> arguments for a SharedKernel project.
    /// </summary>
    /// <param name="project">The project path.</param>
    /// <param name="options">The package options.</param>
    /// <param name="packageDirectory">The package directory.</param>
    /// <param name="enablePackageValidation">The optional package-validation flag.</param>
    /// <param name="baselineVersion">The optional package-validation baseline version.</param>
    /// <returns>The command arguments.</returns>
    public static string[] CreatePackArguments(
        string project,
        SharedKernelPackageOptions options,
        string packageDirectory,
        string? enablePackageValidation = null,
        string? baselineVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

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
        AddOptionalProperty(arguments, "EnablePackageValidation", enablePackageValidation);
        AddOptionalProperty(arguments, "PackageValidationBaselineVersion", baselineVersion);
        return [.. arguments];
    }

    private static bool PackageVersionExists(string packageDirectory, string version) =>
        Directory.EnumerateFiles(packageDirectory, "SharedKernel.*")
            .Select(Path.GetFileName)
            .Any(fileName => fileName is not null && IsPackageVersionFile(fileName, version));

    private static bool IsPackageVersionFile(string fileName, string version) =>
        fileName.EndsWith("." + version + ".nupkg", StringComparison.Ordinal)
        || fileName.EndsWith("." + version + ".snupkg", StringComparison.Ordinal)
        || fileName.EndsWith("." + version + ".symbols.nupkg", StringComparison.Ordinal);

    private static void AddOptionalProperty(List<string> arguments, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments.Add("-p:" + propertyName + "=" + value);
        }
    }
}
