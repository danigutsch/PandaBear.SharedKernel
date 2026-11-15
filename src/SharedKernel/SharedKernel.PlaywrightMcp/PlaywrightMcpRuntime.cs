using PandaBear.Processes;

namespace PandaBear.PlaywrightMcp;

/// <summary>The container engine and options that <see cref="PlaywrightMcpSettings"/> resolve to on this machine.</summary>
/// <param name="Engine">Container engine to use.</param>
/// <param name="ExecutablePath">Path of the container engine executable.</param>
/// <param name="NetworkAccess">Whether the container may access the network.</param>
/// <param name="ImageResponses">How the server returns images.</param>
/// <param name="DockerContext">Docker context name.</param>
/// <param name="DockerEndpoint">Docker endpoint of the active context, or <see langword="null" /> when unknown.</param>
internal sealed record PlaywrightMcpRuntime(
    ContainerEngine Engine,
    string ExecutablePath,
    bool NetworkAccess,
    string ImageResponses,
    string DockerContext = "default",
    string? DockerEndpoint = null)
{
    /// <summary>Chooses the container engine from the settings and the installed executables, and validates the options.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="executableResolver"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Neither Docker nor Podman is available, both are and no engine is selected, or an environment variable has an unsupported value.</exception>
    /// <exception cref="InvalidOperationException">The selected container runtime path is not absolute.</exception>
    public static PlaywrightMcpRuntime Resolve(PlaywrightMcpSettings settings, IExecutableResolver executableResolver)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(executableResolver);

        var requestedEngine = settings.Engine;
        var dockerPath = executableResolver.Find("docker");
        var podmanPath = executableResolver.Find("podman");
        var (engine, executablePath) = requestedEngine?.ToUpperInvariant() switch
        {
            "DOCKER" => (ContainerEngine.Docker, RequireExecutable(dockerPath, "Docker")),
            "PODMAN" => (ContainerEngine.Podman, RequireExecutable(podmanPath, "Podman")),
            null or "" when dockerPath is not null && podmanPath is null => (ContainerEngine.Docker, dockerPath),
            null or "" when dockerPath is null && podmanPath is not null => (ContainerEngine.Podman, podmanPath),
            null or "" when dockerPath is not null && podmanPath is not null => throw new ArgumentException(
                "Set PLAYWRIGHT_MCP_ENGINE to docker or podman when both are installed."),
            null or "" => throw new ArgumentException("Playwright MCP requires Docker or Podman."),
            _ => throw new ArgumentException("PLAYWRIGHT_MCP_ENGINE must be docker or podman.")
        };

        RejectRemoteOverrides(engine, settings);

        var networkAccess = settings.NetworkAccess switch
        {
            null or "" or "0" => false,
            "1" => true,
            _ => throw new ArgumentException("PLAYWRIGHT_MCP_NETWORK_ACCESS must be 0 or 1.")
        };
        var imageResponses = settings.ImageResponses switch
        {
            null or "" or "omit" => "omit",
            "allow" => "allow",
            _ => throw new ArgumentException("PLAYWRIGHT_MCP_IMAGE_RESPONSES must be allow or omit.")
        };

        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new InvalidOperationException("The selected container runtime path must be absolute.");
        }

        return new PlaywrightMcpRuntime(engine, executablePath, networkAccess, imageResponses);
    }

    private static string RequireExecutable(string? path, string name)
    {
        return path ?? throw new ArgumentException($"{name} is not installed.");
    }

    private static void RejectRemoteOverrides(
        ContainerEngine engine,
        PlaywrightMcpSettings settings)
    {
        var hasOverride = engine switch
        {
            ContainerEngine.Docker => HasValue(settings.DockerHost) || HasValue(settings.DockerContext),
            ContainerEngine.Podman => HasValue(settings.ContainerHost) || HasValue(settings.ContainerConnection),
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported container engine.")
        };

        if (hasOverride)
        {
            var name = engine == ContainerEngine.Docker ? "Docker host or context" : "Podman host or connection";
            throw new ArgumentException($"Refusing {name} overrides for Playwright MCP.");
        }

        static bool HasValue(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
