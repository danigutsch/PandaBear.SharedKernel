using System.Text.Json;
using PandaBear.Processes;

namespace PandaBear.Repositories;

/// <summary>Discovers file-based apps and uses the selected SDK to evaluate their NuGet inputs without running them.</summary>
internal static class NuGetFileApps
{
    /// <summary>Finds tracked C# files with build or dependency directives in their headers.</summary>
    internal static async Task<string[]> Find(string root, CancellationToken ct)
    {
        var files = await GitTrackedFiles.Read(root, ["*.cs"], ct).ConfigureAwait(false);
        return files.Where(file => HasFileAppDirective(root, file, ct)).ToArray();
    }

    /// <summary>Reads the direct references and defining central versions from the SDK's virtual project.</summary>
    /// <exception cref="InvalidOperationException">The virtual project or its required package metadata cannot be evaluated.</exception>
    internal static async Task<NuGetFileApp> Evaluate(string root, string file, CancellationToken ct)
    {
        var path = NuGetTrialWorkspace.RepositoryPath(root, file);
        // A verbose SDK context can print a telemetry preamble before the JSON.
        var result = await NuGetRestoreGraphComparison.RunDotnet(root, new BoundedProcessRunner(),
            ["build", path, "--no-restore", "--nologo", "--disable-build-servers",
                "-getProperty:ManagePackageVersionsCentrally,TargetFramework,RestorePackagesWithLockFile,NuGetLockFilePath,MSBuildProjectFullPath",
                "-getItem:PackageReference,PackageVersion,ProjectReference"], ct,
            environment: new Dictionary<string, string> { ["DOTNET_CLI_CONTEXT_VERBOSE"] = "false" }).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"The SDK could not evaluate the file-based app {file}.");
        }

        using var json = JsonDocument.Parse(result.StandardOutput);
        var properties = json.RootElement.GetProperty("Properties");
        var framework = Value(properties, "TargetFramework");
        var projectPath = Value(properties, "MSBuildProjectFullPath");
        if (string.IsNullOrWhiteSpace(framework))
        {
            throw new InvalidOperationException($"The SDK did not report a target framework for the file-based app {file}.");
        }

        if (projectPath is null)
        {
            throw new InvalidOperationException($"The SDK did not report an MSBuild project path for the file-based app {file}.");
        }

        var items = json.RootElement.GetProperty("Items");
        var central = string.Equals(Value(properties, "ManagePackageVersionsCentrally"), "true", StringComparison.OrdinalIgnoreCase);
        var versions = items.GetProperty("PackageVersion").EnumerateArray().ToLookup(item => Value(item, "Identity"), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, (string? Version, string? CentralFile)> packages = new(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in items.GetProperty("PackageReference").EnumerateArray())
        {
            var id = Value(reference, "Identity") ?? throw new InvalidOperationException($"An evaluated package reference in {file} has no identity.");
            var version = Value(reference, "VersionOverride") ?? Value(reference, "Version");
            string? centralFile = null;
            if (central && version is null)
            {
                var declarations = versions[id].ToArray();
                if (declarations.Length > 1)
                {
                    throw new InvalidOperationException($"The central version declaration for {id} in {file} is ambiguous.");
                }

                if (declarations.Length == 1)
                {
                    version = Value(declarations[0], "Version");
                    centralFile = Value(declarations[0], "DefiningProjectFullPath")
                        ?? throw new InvalidOperationException($"The SDK did not report the central version source for {id} in {file}.");
                    centralFile = Path.GetRelativePath(root, centralFile).Replace('\\', '/');
                }
            }

            packages[id] = (version, centralFile);
        }

        var lockFile = Value(properties, "NuGetLockFilePath");
        if (lockFile is not null || string.Equals(Value(properties, "RestorePackagesWithLockFile"), "true", StringComparison.OrdinalIgnoreCase))
        {
            var lockPath = Path.GetFullPath(lockFile ?? "packages.lock.json", Path.GetDirectoryName(path)!);
            lockFile = Path.GetRelativePath(root, lockPath).Replace('\\', '/');
        }

        var virtualProject = Path.GetRelativePath(root, projectPath).Replace('\\', '/');
        return new(file, virtualProject, framework, packages, items.GetProperty("ProjectReference").GetArrayLength() != 0, lockFile);
    }

    /// <summary>Checks existing file-app assets against fresh SDK restore inputs without building or running the app.</summary>
    internal static async Task<NuGetRestoreProject> ReadGraph(string root, string file, string[] restoreOptions, CancellationToken ct)
    {
        var directory = Directory.CreateTempSubdirectory("sharedkernel-file-app-graph-");
        try
        {
            var path = NuGetTrialWorkspace.RepositoryPath(root, file);
            var spec = Path.Combine(directory.FullName, "restore.json");
            var result = await NuGetRestoreGraphComparison.RunDotnet(root, new BoundedProcessRunner(),
                ["build", path, "--no-restore", "--nologo", "--disable-build-servers", "-t:GenerateRestoreGraphFile",
                    "-p:RestoreGraphOutputPath=" + spec, "-p:RestoreLockedMode=true", .. restoreOptions], ct).ConfigureAwait(false);
            if (result.ExitCode == 0 && File.Exists(spec))
            {
                var projects = await NuGetRestoreGraphReader.Read(root, spec, ct).ConfigureAwait(false);
                if (projects.TryGetValue(file + ".csproj", out var project))
                {
                    return project.Unavailable is null
                        ? project
                        : project with { Unavailable = "The file-based app has no current restore evidence inside the checkout." };
                }
            }

            return new(false, null, "The SDK could not evaluate the file-based app restore graph.");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException
            or InvalidOperationException or KeyNotFoundException or JsonException or TimeoutException)
        {
            return new(false, null, "The file-based app restore evidence could not be read.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static string? Value(JsonElement element, string name)
    {
        var value = element.TryGetProperty(name, out var property) ? property.GetString() : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool HasFileAppDirective(string root, string file, CancellationToken ct)
    {
        var path = NuGetTrialWorkspace.RepositoryPath(root, file);
        if (!File.Exists(path))
        {
            return false;
        }

        var inComment = false;
        foreach (var line in File.ReadLines(path))
        {
            ct.ThrowIfCancellationRequested();
            var text = line.TrimStart();
            if (inComment || text.StartsWith("/*", StringComparison.Ordinal))
            {
                var end = text.IndexOf("*/", StringComparison.Ordinal);
                if (end < 0)
                {
                    inComment = true;
                    continue;
                }

                inComment = false;
                text = text[(end + 2)..].TrimStart();
            }

            if (text.Length == 0 || text.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (!text.StartsWith('#', StringComparison.Ordinal))
            {
                return false;
            }

            var separator = text.AsSpan().IndexOfAny(' ', '\t');
            if (separator >= 0 && text[..separator] is "#:package" or "#:include" or "#:project" or "#:sdk" or "#:property")
            {
                return true;
            }
        }

        return false;
    }
}
