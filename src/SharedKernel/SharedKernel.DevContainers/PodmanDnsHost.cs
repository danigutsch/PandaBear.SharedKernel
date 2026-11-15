using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.DevContainers;

/// <summary>
/// Describes the machine that <c>verify-podman-dns</c> inspects.
/// </summary>
/// <param name="PodmanExecutable">The absolute Podman path, or <see langword="null"/> when Podman is not installed.</param>
/// <param name="StatExecutable">The absolute GNU stat path, or <see langword="null"/> when it is unavailable.</param>
/// <param name="ProcRoot">The process filesystem root.</param>
/// <param name="IsLinux">Whether the host runs Linux.</param>
/// <param name="RunProcess">Runs one inspection process and captures its bounded output.</param>
public sealed record PodmanDnsHost(
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
            PathExecutableResolver.Default.Find("podman"),
            new PathExecutableResolver(string.Concat("/usr/bin", Path.PathSeparator, "/bin"), isWindows: false).Find("stat"),
            "/proc",
            OperatingSystem.IsLinux(),
            PodmanDnsPreflight.RunProcess);
}
