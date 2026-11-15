using System.Runtime.InteropServices;
using PandaBear.Processes;

namespace PandaBear.DevContainers;

/// <summary>
/// Supplies the host services that <c>verify-devcontainer</c> uses.
/// </summary>
/// <param name="DockerExecutable">The absolute Docker path that removes the container, or <see langword="null"/> when Docker is missing.</param>
/// <param name="IsLinuxX64">Whether the pinned Dev Container CLI can be bootstrapped on this host.</param>
/// <param name="HttpClient">The client that downloads the pinned CLI, or <see langword="null"/> to create one.</param>
/// <param name="Pins">The pinned Node.js runtime and CLI package.</param>
public sealed record DevcontainerHost(string? DockerExecutable, bool IsLinuxX64, HttpClient? HttpClient, DevcontainerCliPins Pins)
{
    /// <summary>Gets the services of the current machine.</summary>
    public static DevcontainerHost Current() =>
        new(
            PathExecutableResolver.Default.Find("docker"),
            OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64,
            null,
            DevcontainerCliPins.Default);
}
