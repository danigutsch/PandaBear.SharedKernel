namespace SharedKernel.DevContainers.Tests;

/// <summary>Tests the archive checks of the pinned Node.js and Dev Container CLI bootstrap.</summary>
public sealed class DevcontainerCliArchiveTests
{
    [Fact]
    public async Task Node_archive_whose_node_is_a_link_is_rejected()
    {
        // Arrange
        var node = DevcontainerCliArchive.Create(new DevcontainerCliArchiveEntry($"node-v{DevcontainerCliArchive.NodeVersion}-linux-x64/bin/node", LinkTarget: "/bin/sh"));

        Func<Task> action = () => DevcontainerCliBootstrapRun.Bootstrap(node, DevcontainerCliArchive.CliArchive());

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("regular file", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Node_archive_without_node_is_rejected()
    {
        // Arrange
        var node = DevcontainerCliArchive.Create(new DevcontainerCliArchiveEntry($"node-v{DevcontainerCliArchive.NodeVersion}-linux-x64/README.md", "readme"));

        Func<Task> action = () => DevcontainerCliBootstrapRun.Bootstrap(node, DevcontainerCliArchive.CliArchive());

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("bin/node", StringComparison.Ordinal);
    }

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

        Func<Task> action = () => DevcontainerCliBootstrapRun.Bootstrap(DevcontainerCliArchive.NodeArchive("#!/bin/sh\n"), cli);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("unsafe path", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_archive_link_is_rejected()
    {
        // Arrange
        var cli = DevcontainerCliArchive.Create(
            new DevcontainerCliArchiveEntry("package/devcontainer.js", "require('./dist');"),
            new DevcontainerCliArchiveEntry("package/link.js", LinkTarget: "/etc/passwd"));

        Func<Task> action = () => DevcontainerCliBootstrapRun.Bootstrap(DevcontainerCliArchive.NodeArchive("#!/bin/sh\n"), cli);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("regular file", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_archive_without_the_entry_point_is_rejected()
    {
        // Arrange
        var cli = DevcontainerCliArchive.Create(new DevcontainerCliArchiveEntry("package/package.json", "{}"));

        Func<Task> action = () => DevcontainerCliBootstrapRun.Bootstrap(DevcontainerCliArchive.NodeArchive("#!/bin/sh\n"), cli);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("package/devcontainer.js", StringComparison.Ordinal);
    }
}
