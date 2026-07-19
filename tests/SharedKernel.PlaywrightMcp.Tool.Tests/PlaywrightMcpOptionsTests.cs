namespace SharedKernel.PlaywrightMcp.Tool.Tests;

[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class PlaywrightMcpOptionsTests
{
    [Fact]
    public void Selects_the_only_installed_engine_with_secure_defaults()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        var dockerPath = PlaywrightMcpTestEnvironment.GetExecutablePath("docker");
        environment.SetExecutable("docker", dockerPath);

        // Act
        var options = PlaywrightMcpOptions.Parse(
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable);

        // Assert
        options.Engine.ShouldBe(ContainerEngine.Docker);
        options.ExecutablePath.ShouldBe(dockerPath);
        options.NetworkAccess.ShouldBeFalse();
        options.ImageResponses.ShouldBe("omit");
    }

    [Fact]
    public void Requires_explicit_selection_when_both_engines_are_installed()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        environment.SetExecutable("podman", PlaywrightMcpTestEnvironment.GetExecutablePath("podman"));

        // Act
        Action parse = () => PlaywrightMcpOptions.Parse(
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable);

        // Assert
        var exception = parse.ShouldThrow<ArgumentException>();
        exception.Message.ShouldBe("Set PLAYWRIGHT_MCP_ENGINE to docker or podman when both are installed.");
    }

    [Theory]
    [InlineData("docker", "DOCKER_HOST", "tcp://remote.example:2375", "Refusing Docker host or context overrides for Playwright MCP.")]
    [InlineData("docker", "DOCKER_CONTEXT", "remote", "Refusing Docker host or context overrides for Playwright MCP.")]
    [InlineData("podman", "CONTAINER_HOST", "ssh://remote.example", "Refusing Podman host or connection overrides for Playwright MCP.")]
    [InlineData("podman", "CONTAINER_CONNECTION", "remote", "Refusing Podman host or connection overrides for Playwright MCP.")]
    public void Rejects_remote_engine_overrides(
        string engine,
        string variable,
        string value,
        string expectedMessage)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable(engine, PlaywrightMcpTestEnvironment.GetExecutablePath(engine));
        environment.SetEnvironmentVariable("PLAYWRIGHT_MCP_ENGINE", engine);
        environment.SetEnvironmentVariable(variable, value);

        // Act
        Action parse = () => PlaywrightMcpOptions.Parse(
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable);

        // Assert
        var exception = parse.ShouldThrow<ArgumentException>();
        exception.Message.ShouldBe(expectedMessage);
    }

    [Fact]
    public void Requires_explicit_opt_in_for_network_and_image_responses()
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("podman", PlaywrightMcpTestEnvironment.GetExecutablePath("podman"));
        environment.SetEnvironmentVariable("PLAYWRIGHT_MCP_NETWORK_ACCESS", "1");
        environment.SetEnvironmentVariable("PLAYWRIGHT_MCP_IMAGE_RESPONSES", "allow");

        // Act
        var options = PlaywrightMcpOptions.Parse(
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable);

        // Assert
        options.Engine.ShouldBe(ContainerEngine.Podman);
        options.NetworkAccess.ShouldBeTrue();
        options.ImageResponses.ShouldBe("allow");
    }

    [Theory]
    [InlineData("PLAYWRIGHT_MCP_NETWORK_ACCESS", "yes", "PLAYWRIGHT_MCP_NETWORK_ACCESS must be 0 or 1.")]
    [InlineData("PLAYWRIGHT_MCP_IMAGE_RESPONSES", "yes", "PLAYWRIGHT_MCP_IMAGE_RESPONSES must be allow or omit.")]
    public void Rejects_invalid_security_opt_in_values(
        string variable,
        string value,
        string expectedMessage)
    {
        // Arrange
        var environment = new PlaywrightMcpTestEnvironment();
        environment.SetExecutable("docker", PlaywrightMcpTestEnvironment.GetExecutablePath("docker"));
        environment.SetEnvironmentVariable(variable, value);

        // Act
        Action parse = () => PlaywrightMcpOptions.Parse(
            environment.GetEnvironmentVariable,
            environment.ResolveExecutable);

        // Assert
        var exception = parse.ShouldThrow<ArgumentException>();
        exception.Message.ShouldBe(expectedMessage);
    }
}
