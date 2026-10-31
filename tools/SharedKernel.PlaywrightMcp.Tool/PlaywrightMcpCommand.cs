using System.Diagnostics;

namespace PandaBear.PlaywrightMcp.Tool;

/// <summary>Builds the process start information for each container command that runs Playwright MCP.</summary>
internal static class PlaywrightMcpCommand
{
    /// <summary>Digest-pinned Playwright MCP container image reference.</summary>
    internal const string Image = "mcr.microsoft.com/playwright/mcp:v0.0.78@sha256:3d871c22ea2d4cca0966e2cfb1860e1cb03eb7353725a3d6cffd133296fb04eb";

    /// <summary>Creates the start information that shows the active Docker context.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The engine of <paramref name="options"/> is not Docker.</exception>
    public static ProcessStartInfo CreateDockerContextShowStartInfo(PlaywrightMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Engine != ContainerEngine.Docker)
        {
            throw new ArgumentException("Docker context inspection requires the Docker engine.", nameof(options));
        }

        var startInfo = CreateBaseStartInfo(options);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        AddArguments(startInfo, "context", "show");
        return startInfo;
    }

    /// <summary>Creates the start information that probes the Docker context for its endpoint.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The engine of <paramref name="options"/> is not Docker.</exception>
    public static ProcessStartInfo CreateDockerContextProbeStartInfo(PlaywrightMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Engine != ContainerEngine.Docker)
        {
            throw new ArgumentException("Docker context inspection requires the Docker engine.", nameof(options));
        }

        var startInfo = CreateBaseStartInfo(options);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        AddArguments(
            startInfo,
            "--context",
            options.DockerContext,
            "context",
            "inspect",
            options.DockerContext,
            "--format",
            "{{.Endpoints.docker.Host}}");
        return startInfo;
    }

    /// <summary>Creates the start information that runs the Playwright MCP container on the selected engine.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="containerName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static ProcessStartInfo CreateRuntimeStartInfo(
        PlaywrightMcpOptions options,
        string containerName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);

        var startInfo = CreateBaseStartInfo(options);
        AddLocalEnginePrefix(startInfo, options);

        AddArguments(
            startInfo,
            "run",
            "--rm",
            "-i",
            "--init",
            $"--name={containerName}",
            "--pull=never",
            "--log-driver=none",
            "--read-only",
            "--tmpfs",
            "/tmp:rw,nosuid,nodev,noexec,size=512m",
            "--tmpfs",
            "/home/node:rw,nosuid,nodev,size=128m,uid=1000,gid=1000,mode=0700",
            "--security-opt=no-new-privileges",
            "--pids-limit=256",
            "--memory=2g",
            "--shm-size=1g",
            "--env",
            "HTTP_PROXY=",
            "--env",
            "HTTPS_PROXY=",
            "--env",
            "ALL_PROXY=",
            "--env",
            "FTP_PROXY=",
            "--env",
            "NO_PROXY=",
            "--env",
            "http_proxy=",
            "--env",
            "https_proxy=",
            "--env",
            "all_proxy=",
            "--env",
            "ftp_proxy=",
            "--env",
            "no_proxy=");

        if (options.Engine == ContainerEngine.Docker)
        {
            AddArguments(startInfo, "--cap-drop=ALL");
        }
        else
        {
            AddArguments(startInfo, "--read-only-tmpfs=false", "--http-proxy=false", "--cap-drop=all");
        }

        if (!options.NetworkAccess)
        {
            AddArguments(startInfo, "--network=none");
        }
        else if (options.Engine == ContainerEngine.Docker)
        {
            AddArguments(startInfo, "--add-host=host.docker.internal:host-gateway");
        }

        AddArguments(startInfo, Image);
        AddArguments(
            startInfo,
            "--isolated",
            "--block-service-workers",
            "--output-dir=/tmp/playwright-output",
            "--output-max-size=10485760",
            $"--image-responses={options.ImageResponses}");
        return startInfo;
    }

    /// <summary>Creates the start information that pulls the Playwright MCP image.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static ProcessStartInfo CreatePullStartInfo(PlaywrightMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var startInfo = CreateBaseStartInfo(options);
        AddLocalEnginePrefix(startInfo, options);
        AddArguments(startInfo, "pull", Image);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        return startInfo;
    }

    /// <summary>Creates the start information that removes the Playwright MCP image.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static ProcessStartInfo CreateImageRemoveStartInfo(PlaywrightMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var startInfo = CreateBaseStartInfo(options);
        AddLocalEnginePrefix(startInfo, options);
        AddArguments(startInfo, "image", "rm", Image);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        return startInfo;
    }

    /// <summary>Creates the start information that removes the named container.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="containerName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static ProcessStartInfo CreateContainerCleanupStartInfo(
        PlaywrightMcpOptions options,
        string containerName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        var startInfo = CreateBaseStartInfo(options);
        AddLocalEnginePrefix(startInfo, options);
        AddArguments(startInfo, "rm", "--force");
        if (options.Engine == ContainerEngine.Podman)
        {
            AddArguments(startInfo, "--ignore");
        }

        AddArguments(startInfo, containerName);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        return startInfo;
    }

    /// <summary>Creates the start information that checks whether the named container exists.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="containerName"/> is empty or whitespace, or the engine of <paramref name="options"/> is not Docker.</exception>
    public static ProcessStartInfo CreateContainerExistenceProbeStartInfo(
        PlaywrightMcpOptions options,
        string containerName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        if (options.Engine != ContainerEngine.Docker)
        {
            throw new ArgumentException("Container existence inspection requires Docker.", nameof(options));
        }

        var startInfo = CreateBaseStartInfo(options);
        AddLocalEnginePrefix(startInfo, options);
        AddArguments(startInfo, "container", "ls", "--all", "--quiet", "--filter", $"name=^/{containerName}$");
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        return startInfo;
    }

    /// <summary>Creates the start information that checks whether Podman runs rootless.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The engine of <paramref name="options"/> is not Podman.</exception>
    public static ProcessStartInfo CreatePodmanRootlessProbeStartInfo(PlaywrightMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Engine != ContainerEngine.Podman)
        {
            throw new ArgumentException("Rootless inspection requires the Podman engine.", nameof(options));
        }

        var startInfo = CreateBaseStartInfo(options);
        AddArguments(startInfo, "--remote=false", "info", "--format", "{{.Host.Security.Rootless}}");
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        return startInfo;
    }

    /// <summary>Returns whether the Docker endpoint refers to a local Unix socket or Windows named pipe.</summary>
    public static bool IsLocalDockerEndpoint(string? endpoint)
    {
        return endpoint?.Trim() is { } value
            && (value.StartsWith("unix://", StringComparison.Ordinal)
                || value.StartsWith("npipe:////./pipe/", StringComparison.OrdinalIgnoreCase));
    }

    private static ProcessStartInfo CreateBaseStartInfo(PlaywrightMcpOptions options)
    {
        var startInfo = new ProcessStartInfo(options.ExecutablePath)
        {
            UseShellExecute = false
        };

        if (options.Engine == ContainerEngine.Docker)
        {
            startInfo.Environment.Remove("DOCKER_HOST");
            startInfo.Environment.Remove("DOCKER_CONTEXT");
        }
        else
        {
            startInfo.Environment.Remove("CONTAINER_HOST");
            startInfo.Environment.Remove("CONTAINER_CONNECTION");
        }

        return startInfo;
    }

    private static void AddLocalEnginePrefix(ProcessStartInfo startInfo, PlaywrightMcpOptions options)
    {
        if (options.Engine == ContainerEngine.Docker)
        {
            if (string.IsNullOrWhiteSpace(options.DockerEndpoint))
            {
                throw new InvalidOperationException("The Docker endpoint must be validated before use.");
            }

            AddArguments(startInfo, "--host", options.DockerEndpoint);
        }
        else
        {
            AddArguments(startInfo, "--remote=false");
        }
    }

    private static void AddArguments(ProcessStartInfo startInfo, params ReadOnlySpan<string> arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }
}
