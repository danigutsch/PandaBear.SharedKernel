namespace PandaBear.DevContainers.Tests;

/// <summary>Runs the Dev Container CLI bootstrap against scripted archive downloads.</summary>
internal static class DevcontainerCliBootstrapRun
{
    /// <summary>Runs the bootstrap against the given Node.js and CLI archives in a temporary directory, then asserts that it left no files behind.</summary>
    public static async Task Bootstrap(byte[] node, byte[] cli)
    {
        using var directory = new TemporaryDirectory();
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(DevcontainerCliArchive.Response(node));
        handler.Enqueue(DevcontainerCliArchive.Response(cli));
        using var httpClient = new HttpClient(handler);
        try
        {
            await DevcontainerCliBootstrap.Ensure(directory.Path, DevcontainerCliArchive.Pins(node, cli), httpClient, TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.EnumerateFiles(directory.Path, "*", SearchOption.AllDirectories).ShouldBeEmpty();
        }
    }
}
