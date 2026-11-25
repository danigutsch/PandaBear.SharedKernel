namespace SharedKernel.PlaywrightMcp.Tests;

/// <summary>Tests how the settings resolve to a container engine and the security options.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, "unit")]
public sealed class PlaywrightMcpRuntimeTests
{
    [Fact]
    public void Selects_the_only_installed_engine_with_secure_defaults()
    {
        // Arrange
        var environment = new FakeMachine();
        var dockerPath = FakeMachine.GetExecutablePath("docker");
        environment.SetExecutable("docker", dockerPath);

        // Act
        var options = PlaywrightMcpRuntime.Resolve(environment.Settings, environment);

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
        var environment = new FakeMachine();
        environment.SetExecutable("docker", FakeMachine.GetExecutablePath("docker"));
        environment.SetExecutable("podman", FakeMachine.GetExecutablePath("podman"));

        // Act
        Action parse = () => PlaywrightMcpRuntime.Resolve(environment.Settings, environment);

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
        var environment = new FakeMachine();
        environment.SetExecutable(engine, FakeMachine.GetExecutablePath(engine));
        environment.SetEnvironmentVariable("PLAYWRIGHT_MCP_ENGINE", engine);
        environment.SetEnvironmentVariable(variable, value);

        // Act
        Action parse = () => PlaywrightMcpRuntime.Resolve(environment.Settings, environment);

        // Assert
        var exception = parse.ShouldThrow<ArgumentException>();
        exception.Message.ShouldBe(expectedMessage);
    }

    [Fact]
    public void Requires_explicit_opt_in_for_network_and_image_responses()
    {
        // Arrange
        var environment = new FakeMachine();
        environment.SetExecutable("podman", FakeMachine.GetExecutablePath("podman"));
        environment.SetEnvironmentVariable("PLAYWRIGHT_MCP_NETWORK_ACCESS", "1");
        environment.SetEnvironmentVariable("PLAYWRIGHT_MCP_IMAGE_RESPONSES", "allow");

        // Act
        var options = PlaywrightMcpRuntime.Resolve(environment.Settings, environment);

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
        var environment = new FakeMachine();
        environment.SetExecutable("docker", FakeMachine.GetExecutablePath("docker"));
        environment.SetEnvironmentVariable(variable, value);

        // Act
        Action parse = () => PlaywrightMcpRuntime.Resolve(environment.Settings, environment);

        // Assert
        var exception = parse.ShouldThrow<ArgumentException>();
        exception.Message.ShouldBe(expectedMessage);
    }
}
