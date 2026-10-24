using System.Diagnostics;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Describes the machine that <c>verify-podman-dns</c> inspects.
/// </summary>
/// <param name="PodmanExecutable">The absolute Podman path, or <see langword="null"/> when Podman is not installed.</param>
/// <param name="StatExecutable">The absolute GNU stat path, or <see langword="null"/> when it is unavailable.</param>
/// <param name="ProcRoot">The process filesystem root.</param>
/// <param name="IsLinux">Whether the host runs Linux.</param>
/// <param name="RunProcess">Runs one inspection process and captures its bounded output.</param>
internal sealed record PodmanDnsHost(
    string? PodmanExecutable,
    string? StatExecutable,
    string ProcRoot,
    bool IsLinux,
    Func<ProcessStartInfo, CancellationToken, Task<PodmanCommandResult>> RunProcess)
{
    /// <summary>
    /// Describes the current machine, resolving Podman from <c>PATH</c> and stat from the system directories.
    /// </summary>
    public static PodmanDnsHost Current() =>
        new(
            ExecutableResolver.Resolve("podman", Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows()),
            ExecutableResolver.Resolve("stat", string.Concat("/usr/bin", Path.PathSeparator, "/bin"), isWindows: false),
            "/proc",
            OperatingSystem.IsLinux(),
            PodmanDnsPreflightCommand.RunProcess);
}
