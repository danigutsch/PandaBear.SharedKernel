namespace PandaBear.PlaywrightMcp;

/// <summary>
/// What the host asks of <see cref="PlaywrightMcpServer"/>, in the form the <c>PLAYWRIGHT_MCP_*</c> environment variables
/// spell it. The server validates the values: an unsupported value fails the operation with exit code 2.
/// </summary>
public sealed record PlaywrightMcpSettings
{
    /// <summary>Gets <c>docker</c> or <c>podman</c>, or <see langword="null"/> to use the only installed engine (<c>PLAYWRIGHT_MCP_ENGINE</c>).</summary>
    public string? Engine { get; init; }

    /// <summary>Gets <c>1</c> to enable container networking, or <c>0</c> or <see langword="null"/> to keep it off (<c>PLAYWRIGHT_MCP_NETWORK_ACCESS</c>).</summary>
    public string? NetworkAccess { get; init; }

    /// <summary>Gets <c>allow</c> to send images to the MCP client, or <c>omit</c> or <see langword="null"/> to omit them (<c>PLAYWRIGHT_MCP_IMAGE_RESPONSES</c>).</summary>
    public string? ImageResponses { get; init; }

    /// <summary>Gets the host's <c>DOCKER_HOST</c>, which the server refuses because it can point at a remote engine.</summary>
    public string? DockerHost { get; init; }

    /// <summary>Gets the host's <c>DOCKER_CONTEXT</c>, which the server refuses because it can point at a remote engine.</summary>
    public string? DockerContext { get; init; }

    /// <summary>Gets the host's <c>CONTAINER_HOST</c>, which the server refuses because it can point at a remote engine.</summary>
    public string? ContainerHost { get; init; }

    /// <summary>Gets the host's <c>CONTAINER_CONNECTION</c>, which the server refuses because it can point at a remote engine.</summary>
    public string? ContainerConnection { get; init; }

    /// <summary>Reads the settings from environment variables.</summary>
    /// <param name="getEnvironmentVariable">Reads a variable by name, such as <see cref="Environment.GetEnvironmentVariable(string)"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getEnvironmentVariable"/> is <see langword="null"/>.</exception>
    public static PlaywrightMcpSettings FromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        return new PlaywrightMcpSettings
        {
            Engine = getEnvironmentVariable("PLAYWRIGHT_MCP_ENGINE"),
            NetworkAccess = getEnvironmentVariable("PLAYWRIGHT_MCP_NETWORK_ACCESS"),
            ImageResponses = getEnvironmentVariable("PLAYWRIGHT_MCP_IMAGE_RESPONSES"),
            DockerHost = getEnvironmentVariable("DOCKER_HOST"),
            DockerContext = getEnvironmentVariable("DOCKER_CONTEXT"),
            ContainerHost = getEnvironmentVariable("CONTAINER_HOST"),
            ContainerConnection = getEnvironmentVariable("CONTAINER_CONNECTION"),
        };
    }
}
