using System.Diagnostics;

namespace SharedKernel.PlaywrightMcp.Tool;

internal static class PlaywrightMcpCommand
{
    internal const string Image = "mcr.microsoft.com/playwright/mcp:v0.0.78@sha256:3d871c22ea2d4cca0966e2cfb1860e1cb03eb7353725a3d6cffd133296fb04eb";

    private static readonly HashSet<string> AllowedMcpArguments = new(StringComparer.Ordinal)
    {
        "--help",
        "--version"
    };

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

    public static ProcessStartInfo CreateRuntimeStartInfo(
        PlaywrightMcpOptions options,
        IReadOnlyList<string> mcpArguments,
        string containerName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(mcpArguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ValidateMcpArguments(mcpArguments);

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
            "--output-max-size=10485760");
        foreach (var argument in mcpArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add($"--image-responses={options.ImageResponses}");
        return startInfo;
    }

    public static ProcessStartInfo CreatePullStartInfo(PlaywrightMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var startInfo = CreateBaseStartInfo(options);
        AddLocalEnginePrefix(startInfo, options);
        AddArguments(startInfo, "pull", Image);
        return startInfo;
    }

    public static ProcessStartInfo CreateImageRemoveStartInfo(PlaywrightMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var startInfo = CreateBaseStartInfo(options);
        AddLocalEnginePrefix(startInfo, options);
        AddArguments(startInfo, "image", "rm", Image);
        return startInfo;
    }

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

    private static void ValidateMcpArguments(IReadOnlyList<string> arguments)
    {
        var invalidArgument = arguments.FirstOrDefault(argument => !AllowedMcpArguments.Contains(argument));
        if (invalidArgument is not null)
        {
            throw new ArgumentException($"Playwright MCP argument is not allowed: {invalidArgument}", nameof(arguments));
        }
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
