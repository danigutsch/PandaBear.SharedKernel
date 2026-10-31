namespace PandaBear.PlaywrightMcp.Tool;

/// <summary>Resolved settings for running the Playwright MCP container.</summary>
/// <param name="Engine">Container engine to use.</param>
/// <param name="ExecutablePath">Path of the container engine executable.</param>
/// <param name="NetworkAccess">Whether the container may access the network.</param>
/// <param name="ImageResponses">How the server returns images.</param>
/// <param name="DockerContext">Docker context name.</param>
/// <param name="DockerEndpoint">Docker endpoint of the active context, or <see langword="null" /> when unknown.</param>
internal sealed record PlaywrightMcpOptions(
    ContainerEngine Engine,
    string ExecutablePath,
    bool NetworkAccess,
    string ImageResponses,
    string DockerContext = "default",
    string? DockerEndpoint = null)
{
    /// <summary>Reads the environment and available executables to choose a container engine and build the options.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="getEnvironmentVariable"/> or <paramref name="resolveExecutable"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Neither Docker nor Podman is available, both are and no engine is selected, or an environment variable has an unsupported value.</exception>
    /// <exception cref="InvalidOperationException">The selected container runtime path is not absolute.</exception>
    public static PlaywrightMcpOptions Parse(
        Func<string, string?> getEnvironmentVariable,
        Func<string, string?> resolveExecutable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(resolveExecutable);

        var requestedEngine = getEnvironmentVariable("PLAYWRIGHT_MCP_ENGINE");
        var dockerPath = resolveExecutable("docker");
        var podmanPath = resolveExecutable("podman");
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

        RejectRemoteOverrides(engine, getEnvironmentVariable);

        var networkAccess = getEnvironmentVariable("PLAYWRIGHT_MCP_NETWORK_ACCESS") switch
        {
            null or "" or "0" => false,
            "1" => true,
            _ => throw new ArgumentException("PLAYWRIGHT_MCP_NETWORK_ACCESS must be 0 or 1.")
        };
        var imageResponses = getEnvironmentVariable("PLAYWRIGHT_MCP_IMAGE_RESPONSES") switch
        {
            null or "" or "omit" => "omit",
            "allow" => "allow",
            _ => throw new ArgumentException("PLAYWRIGHT_MCP_IMAGE_RESPONSES must be allow or omit.")
        };

        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new InvalidOperationException("The selected container runtime path must be absolute.");
        }

        return new PlaywrightMcpOptions(engine, executablePath, networkAccess, imageResponses);
    }

    private static string RequireExecutable(string? path, string name)
    {
        return path ?? throw new ArgumentException($"{name} is not installed.");
    }

    private static void RejectRemoteOverrides(
        ContainerEngine engine,
        Func<string, string?> getEnvironmentVariable)
    {
        var hasOverride = engine switch
        {
            ContainerEngine.Docker => HasValue("DOCKER_HOST") || HasValue("DOCKER_CONTEXT"),
            ContainerEngine.Podman => HasValue("CONTAINER_HOST") || HasValue("CONTAINER_CONNECTION"),
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported container engine.")
        };

        if (hasOverride)
        {
            var name = engine == ContainerEngine.Docker ? "Docker host or context" : "Podman host or connection";
            throw new ArgumentException($"Refusing {name} overrides for Playwright MCP.");
        }

        bool HasValue(string variable)
        {
            return !string.IsNullOrWhiteSpace(getEnvironmentVariable(variable));
        }
    }
}
