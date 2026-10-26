using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PandaBear.CI.Tool;

/// <summary>
/// Builds the configured OpenAPI targets with their generate or refresh property, through the dotnet host that runs the
/// tool and with an environment that keeps only what the build needs.
/// </summary>
internal static class OpenApiGeneration
{
    /// <summary>The host environment that <c>PandaBear.OpenApi.OpenApiGenerationMode</c> recognizes.</summary>
    private const string OpenApiGenerationEnvironment = "OpenApiGeneration";

    private const string NuGetPackagesEnvironmentVariable = "NUGET_PACKAGES";
    private static readonly string[] UnixPreservedEnvironmentVariables = ["HOME", "TMPDIR"];
    private static readonly string[] WindowsPreservedEnvironmentVariables = ["TEMP", "TMP", "USERPROFILE", "APPDATA", "LOCALAPPDATA"];

    /// <summary>Builds each target in order, or every configured target when <paramref name="names"/> is empty.</summary>
    /// <param name="root">The repository root, the working directory of each build.</param>
    /// <param name="configuration">The configuration whose targets are built.</param>
    /// <param name="names">The target names.</param>
    /// <param name="refresh">Whether to set the refresh property instead of the generate property.</param>
    /// <param name="buildConfiguration">The <c>-c</c> value, or <see langword="null"/> for the project default.</param>
    /// <param name="runConfigured">Runs a process after its start information is configured.</param>
    /// <param name="error">Receives each build's standard output.</param>
    /// <param name="ct">Cancels the builds.</param>
    /// <exception cref="ArgumentException">A name is not a configured target.</exception>
    /// <exception cref="InvalidOperationException">The configuration defines no target.</exception>
    public static async Task Run(
        string root,
        CiConfiguration configuration,
        IReadOnlyCollection<string> names,
        bool refresh,
        string? buildConfiguration,
        Func<string, string[], string, Action<ProcessStartInfo>, CancellationToken, Task<string>> runConfigured,
        TextWriter error,
        CancellationToken ct)
    {
        var targets = configuration.OpenApi is { Length: > 0 } configured
            ? configured
            : throw CiConfigurationFile.Invalid("$.openApi must define the targets that openapi builds.");
        var selected = names.Count == 0
            ? targets
            : names.Select(name => targets.FirstOrDefault(target => target.Name == name) ?? throw new ArgumentException("Unknown OpenAPI target: " + name + ".")).ToArray();
        var host = DotnetHostPath();
        foreach (var target in selected)
        {
            await error.WriteAsync(await runConfigured(
                host, Arguments(target, refresh, buildConfiguration), root, startInfo => ApplyEnvironment(startInfo, target.PreserveEnvironment), ct).ConfigureAwait(false))
                .ConfigureAwait(false);
        }
    }

    /// <summary>Returns the <c>dotnet build</c> arguments of a target.</summary>
    public static string[] Arguments(OpenApiTargetConfiguration target, bool refresh, string? buildConfiguration) =>
    [
        "build",
        "--no-restore",
        target.Project,
        .. buildConfiguration is null ? Array.Empty<string>() : ["-c", buildConfiguration],
        "-p:" + (refresh ? target.RefreshProperty : target.GenerateProperty) + "=true",
    ];

    /// <summary>Returns the absolute path of the dotnet host that runs this process.</summary>
    /// <exception cref="InvalidOperationException">The runtime does not sit in a dotnet installation.</exception>
    public static string DotnetHostPath()
    {
        var host = Path.GetFullPath(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        return File.Exists(host) ? host : throw new InvalidOperationException("OpenAPI generation must run through a dotnet host.");
    }

    /// <summary>
    /// Replaces the child environment with the profile and temporary directories, the repository NuGet cache when the
    /// parent uses it, the variables that the target lists, a <c>PATH</c> of the dotnet host and system directories, and the
    /// OpenAPI generation settings, which a listed variable cannot replace.
    /// </summary>
    /// <param name="startInfo">The process start information whose environment is replaced.</param>
    /// <param name="preserved">Names of parent environment variables to keep when they have a value.</param>
    /// <exception cref="InvalidOperationException">The process does not start an absolute dotnet host.</exception>
    public static void ApplyEnvironment(ProcessStartInfo startInfo, IReadOnlyCollection<string>? preserved = null)
    {
        if (!Path.IsPathFullyQualified(startInfo.FileName))
        {
            throw new InvalidOperationException("The OpenAPI generation host must be an absolute dotnet path.");
        }

        var isWindows = OperatingSystem.IsWindows();
        var preservedVariables = isWindows ? WindowsPreservedEnvironmentVariables : UnixPreservedEnvironmentVariables;
        var preservedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in preservedVariables)
        {
            if (startInfo.Environment.TryGetValue(variable, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                preservedEnvironment[variable] = value;
            }
        }

        foreach (var variable in preserved ?? [])
        {
            if (startInfo.Environment.TryGetValue(variable, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                preservedEnvironment[variable] = value;
            }
        }

        var repositoryNuGetPackagesPath = Path.Combine(startInfo.WorkingDirectory, ".nuget", "packages");
        var normalizedRepositoryNuGetPackagesPath = Path.TrimEndingDirectorySeparator(
            repositoryNuGetPackagesPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        if (startInfo.Environment.TryGetValue(NuGetPackagesEnvironmentVariable, out var nugetPackagesPath) && !string.IsNullOrWhiteSpace(nugetPackagesPath))
        {
            var normalizedNuGetPackagesPath = Path.TrimEndingDirectorySeparator(nugetPackagesPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
            if (string.Equals(
                normalizedNuGetPackagesPath,
                normalizedRepositoryNuGetPackagesPath,
                isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                preservedEnvironment[NuGetPackagesEnvironmentVariable] = repositoryNuGetPackagesPath;
            }
        }

        startInfo.Environment.Clear();
        foreach (var (variable, value) in preservedEnvironment)
        {
            startInfo.Environment[variable] = value;
        }

        var dotnetDirectory = Path.GetDirectoryName(startInfo.FileName)
            ?? throw new InvalidOperationException("Could not determine the dotnet host directory.");
        if (isWindows)
        {
            var systemRoot = Path.GetDirectoryName(Environment.SystemDirectory)
                ?? throw new InvalidOperationException("Could not determine the Windows system root.");
            startInfo.Environment["SystemRoot"] = systemRoot;
            startInfo.Environment["ComSpec"] = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            startInfo.Environment["PATH"] = string.Join(Path.PathSeparator, dotnetDirectory, Environment.SystemDirectory);
        }
        else
        {
            startInfo.Environment["PATH"] = string.Join(Path.PathSeparator, dotnetDirectory, "/usr/bin", "/bin");
        }

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = OpenApiGenerationEnvironment;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = OpenApiGenerationEnvironment;
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["OTEL_LOGS_EXPORTER"] = "none";
        startInfo.Environment["OTEL_METRICS_EXPORTER"] = "none";
        startInfo.Environment["OTEL_SDK_DISABLED"] = "true";
        startInfo.Environment["OTEL_TRACES_EXPORTER"] = "none";
    }
}
