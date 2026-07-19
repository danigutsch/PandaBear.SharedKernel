namespace SharedKernel.PlaywrightMcp.Tool.Tests;

[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class PlaywrightMcpCommandTests
{
    private const string LocalDockerEndpoint = "unix:///var/run/docker.sock";

    [Fact]
    public void Docker_context_probe_pins_the_selected_current_context()
    {
        // Arrange
        var options = new PlaywrightMcpOptions(
            ContainerEngine.Docker,
            PlaywrightMcpTestEnvironment.GetExecutablePath("docker"),
            false,
            "omit",
            "desktop-linux");

        // Act
        var show = PlaywrightMcpCommand.CreateDockerContextShowStartInfo(options);
        var inspect = PlaywrightMcpCommand.CreateDockerContextProbeStartInfo(options);

        // Assert
        show.ArgumentList.ToArray().ShouldBe(["context", "show"]);
        inspect.ArgumentList.ToArray().ShouldBe(
        [
            "--context",
            "desktop-linux",
            "context",
            "inspect",
            "desktop-linux",
            "--format",
            "{{.Endpoints.docker.Host}}"
        ]);
    }

    [Fact]
    public void Docker_defaults_to_local_offline_network_isolation()
    {
        // Arrange
        var options = new PlaywrightMcpOptions(
            ContainerEngine.Docker,
            PlaywrightMcpTestEnvironment.GetExecutablePath("docker"),
            false,
            "omit",
            DockerEndpoint: LocalDockerEndpoint);

        // Act
        var startInfo = PlaywrightMcpCommand.CreateRuntimeStartInfo(options, [], "test-container");

        // Assert
        startInfo.UseShellExecute.ShouldBeFalse();
        startInfo.RedirectStandardInput.ShouldBeFalse();
        startInfo.RedirectStandardOutput.ShouldBeFalse();
        startInfo.RedirectStandardError.ShouldBeFalse();
        startInfo.ArgumentList.ToArray().ShouldBe(
        [
            "--host",
            "unix:///var/run/docker.sock",
            "run",
            "--rm",
            "-i",
            "--init",
            "--name=test-container",
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
            "no_proxy=",
            "--cap-drop=ALL",
            "--network=none",
            "mcr.microsoft.com/playwright/mcp:v0.0.78@sha256:3d871c22ea2d4cca0966e2cfb1860e1cb03eb7353725a3d6cffd133296fb04eb",
            "--isolated",
            "--block-service-workers",
            "--output-dir=/tmp/playwright-output",
            "--output-max-size=10485760",
            "--image-responses=omit"
        ]);
    }

    [Fact]
    public void Docker_network_opt_in_maps_the_local_host()
    {
        // Arrange
        var options = new PlaywrightMcpOptions(
            ContainerEngine.Docker,
            PlaywrightMcpTestEnvironment.GetExecutablePath("docker"),
            true,
            "allow",
            DockerEndpoint: LocalDockerEndpoint);

        // Act
        var startInfo = PlaywrightMcpCommand.CreateRuntimeStartInfo(options, [], "test-container");

        // Assert
        startInfo.ArgumentList.ShouldContain("--add-host=host.docker.internal:host-gateway");
        startInfo.ArgumentList.ShouldContain("--image-responses=allow");
        startInfo.ArgumentList.ShouldNotContain("--network=none");
    }

    [Fact]
    public void Podman_forces_local_mode_and_disables_implicit_read_only_tmpfs()
    {
        // Arrange
        var options = new PlaywrightMcpOptions(
            ContainerEngine.Podman,
            PlaywrightMcpTestEnvironment.GetExecutablePath("podman"),
            false,
            "omit");

        // Act
        var startInfo = PlaywrightMcpCommand.CreateRuntimeStartInfo(options, [], "test-container");

        // Assert
        startInfo.ArgumentList.ShouldContain("--remote=false");
        startInfo.ArgumentList.ShouldContain("--read-only-tmpfs=false");
        startInfo.ArgumentList.ShouldContain("--http-proxy=false");
        startInfo.ArgumentList.ShouldContain("--cap-drop=all");
        startInfo.ArgumentList.ShouldContain("--network=none");
        startInfo.ArgumentList.ShouldNotContain("--context");
        PlaywrightMcpCommand.CreateContainerCleanupStartInfo(options, "test-container")
            .ArgumentList.ShouldContain("--ignore");
    }

    [Theory]
    [InlineData("docker", "--host", LocalDockerEndpoint)]
    [InlineData("podman", "--remote=false", null)]
    public void Prepare_pulls_the_exact_image_with_the_selected_local_engine(
        string engineName,
        string firstArgument,
        string? secondArgument)
    {
        // Arrange
        var engine = engineName == "docker" ? ContainerEngine.Docker : ContainerEngine.Podman;
        var executable = PlaywrightMcpTestEnvironment.GetExecutablePath(engineName);
        var options = new PlaywrightMcpOptions(
            engine,
            executable,
            false,
            "omit",
            DockerEndpoint: engine == ContainerEngine.Docker ? LocalDockerEndpoint : null);
        var expectedArguments = secondArgument is null
            ? new[] { firstArgument, "pull", PlaywrightMcpCommand.Image }
            : new[] { firstArgument, secondArgument, "pull", PlaywrightMcpCommand.Image };

        // Act
        var startInfo = PlaywrightMcpCommand.CreatePullStartInfo(options);

        // Assert
        startInfo.ArgumentList.ToArray().ShouldBe(expectedArguments);
    }

    [Fact]
    public void Clean_removes_only_the_exact_pinned_image()
    {
        // Arrange
        var options = new PlaywrightMcpOptions(
            ContainerEngine.Docker,
            PlaywrightMcpTestEnvironment.GetExecutablePath("docker"),
            false,
            "omit",
            DockerEndpoint: LocalDockerEndpoint);

        // Act
        var startInfo = PlaywrightMcpCommand.CreateImageRemoveStartInfo(options);

        // Assert
        startInfo.ArgumentList.ToArray().ShouldBe(
        [
            "--host",
            "unix:///var/run/docker.sock",
            "image",
            "rm",
            "mcr.microsoft.com/playwright/mcp:v0.0.78@sha256:3d871c22ea2d4cca0966e2cfb1860e1cb03eb7353725a3d6cffd133296fb04eb"
        ]);
    }

    [Fact]
    public void Docker_cleanup_verifies_that_the_named_container_is_absent()
    {
        // Arrange
        var options = new PlaywrightMcpOptions(
            ContainerEngine.Docker,
            PlaywrightMcpTestEnvironment.GetExecutablePath("docker"),
            false,
            "omit",
            DockerEndpoint: LocalDockerEndpoint);

        // Act
        var cleanup = PlaywrightMcpCommand.CreateContainerCleanupStartInfo(options, "test-container");
        var probe = PlaywrightMcpCommand.CreateContainerExistenceProbeStartInfo(options, "test-container");

        // Assert
        cleanup.ArgumentList.ToArray().ShouldBe(
        [
            "--host",
            "unix:///var/run/docker.sock",
            "rm",
            "--force",
            "test-container"
        ]);
        probe.ArgumentList.ToArray().ShouldBe(
        [
            "--host",
            "unix:///var/run/docker.sock",
            "container",
            "ls",
            "--all",
            "--quiet",
            "--filter",
            "name=^/test-container$"
        ]);
    }

    [Fact]
    public void Podman_rootless_probe_forces_local_mode()
    {
        // Arrange
        var options = new PlaywrightMcpOptions(
            ContainerEngine.Podman,
            PlaywrightMcpTestEnvironment.GetExecutablePath("podman"),
            false,
            "omit");

        // Act
        var startInfo = PlaywrightMcpCommand.CreatePodmanRootlessProbeStartInfo(options);

        // Assert
        startInfo.ArgumentList.ToArray().ShouldBe(
        [
            "--remote=false",
            "info",
            "--format",
            "{{.Host.Security.Rootless}}"
        ]);
    }

    [Fact]
    public void Rejects_unapproved_mcp_arguments()
    {
        // Arrange
        var options = new PlaywrightMcpOptions(
            ContainerEngine.Docker,
            PlaywrightMcpTestEnvironment.GetExecutablePath("docker"),
            false,
            "omit");

        // Act
        Action create = () => PlaywrightMcpCommand.CreateRuntimeStartInfo(
            options,
            ["--allow-unrestricted-file-access"],
            "test-container");

        // Assert
        var exception = create.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("Playwright MCP argument is not allowed", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unix:///var/run/docker.sock")]
    [InlineData("npipe:////./pipe/docker_engine")]
    public void Accepts_local_docker_endpoints(string endpoint)
    {
        // Arrange
        var dockerEndpoint = endpoint;

        // Act
        var isLocal = PlaywrightMcpCommand.IsLocalDockerEndpoint(dockerEndpoint);

        // Assert
        isLocal.ShouldBeTrue();
    }

    [Theory]
    [InlineData("tcp://remote.example:2375")]
    [InlineData("ssh://builder.example")]
    [InlineData("npipe:////remote-host/pipe/docker_engine")]
    [InlineData("npipe://remote-host/pipe/docker_engine")]
    public void Rejects_remote_docker_endpoints(string endpoint)
    {
        // Arrange
        var dockerEndpoint = endpoint;

        // Act
        var isLocal = PlaywrightMcpCommand.IsLocalDockerEndpoint(dockerEndpoint);

        // Assert
        isLocal.ShouldBeFalse();
    }
}
