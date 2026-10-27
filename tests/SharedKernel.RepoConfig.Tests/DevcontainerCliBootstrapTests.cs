using System.Net;
using System.Security.Cryptography;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the download, verification, and extraction of the pinned Node.js and Dev Container CLI archives.</summary>
public sealed class DevcontainerCliBootstrapTests
{
    /// <summary>Verifies that the bootstrap extracts only Node.js and the CLI package, and reuses the cache on a second run.</summary>
    [Fact]
    public async Task Bootstrap_extracts_only_node_and_the_cli_package_and_reuses_the_cache()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        var node = DevcontainerCliArchive.NodeArchive(workspace.CliScript(DevcontainerTestWorkspace.UpScript("{\"outcome\":\"success\",\"containerId\":\"abc123\"}")));
        var cli = DevcontainerCliArchive.CliArchive();
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(DevcontainerCliArchive.Response(node));
        handler.Enqueue(DevcontainerCliArchive.Response(cli));
        using var httpClient = new HttpClient(handler);
        var host = workspace.Host(httpClient: httpClient, pins: DevcontainerCliArchive.Pins(node, cli));
        var prefix = Path.Combine(
            workspace.RootPath, "TestResults", "devcontainer-tools", $"devcontainers-cli-{DevcontainerCliArchive.CliVersion}-node-{DevcontainerCliArchive.NodeVersion}");

        // Act
        var first = await workspace.Run([], host);
        var second = await workspace.Run([], host);

        // Assert
        first.ExitCode.ShouldBe(0, first.Error);
        second.ExitCode.ShouldBe(0, second.Error);
        handler.Requests.Select(static request => request.Message.RequestUri).ToArray().ShouldBe([DevcontainerCliArchive.NodeArchiveUri, DevcontainerCliArchive.CliArchiveUri]);
        Directory.EnumerateFiles(prefix, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(prefix, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray()
            .ShouldBe(
            [
                $"cli/{DevcontainerCliArchive.CliVersion}/package/devcontainer.js",
                $"cli/{DevcontainerCliArchive.CliVersion}/package/package.json",
                $"node/v{DevcontainerCliArchive.NodeVersion}/bin/node",
            ]);
        workspace.CliCalls().Count(static call => call.StartsWith("[up]", StringComparison.Ordinal)).ShouldBe(2);
        var metadata = await File.ReadAllLinesAsync(
            Path.Combine(workspace.RootPath, "TestResults", "devcontainer-smoke", "metadata.txt"),
            TestContext.Current.CancellationToken);
        metadata.ShouldContain($"devcontainer_cli={Path.Combine(prefix, "cli", DevcontainerCliArchive.CliVersion, "package", "devcontainer.js")}");
    }

    /// <summary>Verifies that a Node.js archive with a wrong hash fails before anything is extracted.</summary>
    [Fact]
    public async Task Node_archive_with_a_wrong_hash_fails_before_extracting()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        var node = DevcontainerCliArchive.NodeArchive("#!/bin/sh\n");
        var cli = DevcontainerCliArchive.CliArchive();
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(DevcontainerCliArchive.Response(node));
        handler.Enqueue(DevcontainerCliArchive.Response(cli));
        using var httpClient = new HttpClient(handler);
        var pins = DevcontainerCliArchive.Pins(node, cli) with
        {
            NodeSha256 = Convert.ToHexStringLower(SHA256.HashData("other"u8))
        };

        // Act
        var (exitCode, _, error) = await workspace.Run([], workspace.Host(httpClient: httpClient, pins: pins));

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("SHA-256", StringComparison.Ordinal);
        Directory.Exists(Path.Combine(workspace.RootPath, "TestResults", "devcontainer-tools")).ShouldBeFalse();
        workspace.CliCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies that a CLI archive with a wrong integrity value fails before anything is extracted.</summary>
    [Fact]
    public async Task Cli_archive_with_a_wrong_integrity_fails_before_extracting()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        var node = DevcontainerCliArchive.NodeArchive("#!/bin/sh\n");
        var cli = DevcontainerCliArchive.CliArchive();
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(DevcontainerCliArchive.Response(node));
        handler.Enqueue(DevcontainerCliArchive.Response(cli));
        using var httpClient = new HttpClient(handler);
        var pins = DevcontainerCliArchive.Pins(node, cli) with
        {
            CliIntegrity = "sha512-" + Convert.ToBase64String(SHA512.HashData("other"u8))
        };

        // Act
        var (exitCode, _, error) = await workspace.Run([], workspace.Host(httpClient: httpClient, pins: pins));

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("integrity", StringComparison.Ordinal);
        Directory.Exists(Path.Combine(workspace.RootPath, "TestResults", "devcontainer-tools")).ShouldBeFalse();
    }

    /// <summary>Verifies that a failed download status fails the bootstrap.</summary>
    [Fact]
    public async Task Failed_download_status_fails_the_bootstrap()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new DevcontainerTestWorkspace();
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        using var handler = new TestHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler);
        var pins = DevcontainerCliArchive.Pins(DevcontainerCliArchive.NodeArchive("#!/bin/sh\n"), DevcontainerCliArchive.CliArchive());

        // Act
        var (exitCode, _, error) = await workspace.Run([], workspace.Host(httpClient: httpClient, pins: pins));

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("404", StringComparison.Ordinal);
    }

    /// <summary>Verifies that a Node.js archive whose <c>node</c> entry is a link is rejected.</summary>
    [Fact]
    public async Task Node_archive_whose_node_is_a_link_is_rejected()
    {
        // Arrange
        var node = DevcontainerCliArchive.Create(new DevcontainerCliArchiveEntry($"node-v{DevcontainerCliArchive.NodeVersion}-linux-x64/bin/node", LinkTarget: "/bin/sh"));

        Func<Task> action = () => DevcontainerCliArchive.Bootstrap(node, DevcontainerCliArchive.CliArchive());

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("regular file", StringComparison.Ordinal);
    }

    /// <summary>Verifies that a Node.js archive without a <c>node</c> binary is rejected.</summary>
    [Fact]
    public async Task Node_archive_without_node_is_rejected()
    {
        // Arrange
        var node = DevcontainerCliArchive.Create(new DevcontainerCliArchiveEntry($"node-v{DevcontainerCliArchive.NodeVersion}-linux-x64/README.md", "readme"));

        Func<Task> action = () => DevcontainerCliArchive.Bootstrap(node, DevcontainerCliArchive.CliArchive());

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("bin/node", StringComparison.Ordinal);
    }

    /// <summary>Verifies that a CLI archive entry outside the package directory is rejected.</summary>
    [Theory]
    [InlineData("package/../../escape.js")]
    [InlineData("/escape.js")]
    [InlineData("other/escape.js")]
    [InlineData("package/./escape.js")]
    public async Task Cli_archive_entry_outside_the_package_is_rejected(string name)
    {
        // Arrange
        var cli = DevcontainerCliArchive.Create(
            new DevcontainerCliArchiveEntry("package/devcontainer.js", "require('./dist');"),
            new DevcontainerCliArchiveEntry(name, "escaped"));

        Func<Task> action = () => DevcontainerCliArchive.Bootstrap(DevcontainerCliArchive.NodeArchive("#!/bin/sh\n"), cli);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("unsafe path", StringComparison.Ordinal);
    }

    /// <summary>Verifies that a CLI archive that contains a link is rejected.</summary>
    [Fact]
    public async Task Cli_archive_link_is_rejected()
    {
        // Arrange
        var cli = DevcontainerCliArchive.Create(
            new DevcontainerCliArchiveEntry("package/devcontainer.js", "require('./dist');"),
            new DevcontainerCliArchiveEntry("package/link.js", LinkTarget: "/etc/passwd"));

        Func<Task> action = () => DevcontainerCliArchive.Bootstrap(DevcontainerCliArchive.NodeArchive("#!/bin/sh\n"), cli);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("regular file", StringComparison.Ordinal);
    }

    /// <summary>Verifies that a CLI archive without the entry point is rejected.</summary>
    [Fact]
    public async Task Cli_archive_without_the_entry_point_is_rejected()
    {
        // Arrange
        var cli = DevcontainerCliArchive.Create(new DevcontainerCliArchiveEntry("package/package.json", "{}"));

        Func<Task> action = () => DevcontainerCliArchive.Bootstrap(DevcontainerCliArchive.NodeArchive("#!/bin/sh\n"), cli);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("package/devcontainer.js", StringComparison.Ordinal);
    }
}
