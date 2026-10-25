using System.Formats.Tar;
using System.Net;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Builds gzip-compressed tar archives for Dev Container CLI bootstrap tests.</summary>
internal static class DevcontainerCliArchive
{
    public const string NodeVersion = "24.0.0-test";
    public const string CliVersion = "0.0.0-test";
    public static readonly Uri NodeArchiveUri = new("https://nodejs.test/node.tar.gz");
    public static readonly Uri CliArchiveUri = new("https://registry.test/cli.tgz");

    public static byte[] Create(params DevcontainerCliArchiveEntry[] entries)
    {
        using var archive = new MemoryStream();
        using (var gzip = new GZipStream(archive, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                writer.WriteEntry(entry.LinkTarget is null
                    ? new PaxTarEntry(TarEntryType.RegularFile, entry.Name)
                    {
                        DataStream = new MemoryStream(Encoding.UTF8.GetBytes(entry.Content ?? string.Empty)),
                        Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    }
                    : new PaxTarEntry(TarEntryType.SymbolicLink, entry.Name) { LinkName = entry.LinkTarget });
            }
        }

        return archive.ToArray();
    }

    public static async Task Bootstrap(byte[] node, byte[] cli)
    {
        using var directory = new TemporaryDirectory();
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(Response(node));
        handler.Enqueue(Response(cli));
        using var httpClient = new HttpClient(handler);
        try
        {
            await DevcontainerCliBootstrap.Ensure(directory.Path, Pins(node, cli), httpClient, TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.EnumerateFiles(directory.Path, "*", SearchOption.AllDirectories).ShouldBeEmpty();
        }
    }

    public static byte[] NodeArchive(string nodeScript) =>
        Create(
            new DevcontainerCliArchiveEntry($"node-v{NodeVersion}-linux-x64/bin/node", nodeScript),
            new DevcontainerCliArchiveEntry($"node-v{NodeVersion}-linux-x64/bin/npm", LinkTarget: "../lib/node_modules/npm/bin/npm-cli.js"),
            new DevcontainerCliArchiveEntry($"node-v{NodeVersion}-linux-x64/lib/node_modules/npm/bin/npm-cli.js", "npm"),
            new DevcontainerCliArchiveEntry($"node-v{NodeVersion}-linux-x64/README.md", "readme"));

    public static byte[] CliArchive() =>
        Create(
            new DevcontainerCliArchiveEntry("package/devcontainer.js", "require('./dist');"),
            new DevcontainerCliArchiveEntry("package/package.json", "{}"));

    public static DevcontainerCliPins Pins(byte[] node, byte[] cli) =>
        new(
            NodeVersion,
            NodeArchiveUri,
            Convert.ToHexStringLower(SHA256.HashData(node)),
            CliVersion,
            CliArchiveUri,
            "sha512-" + Convert.ToBase64String(SHA512.HashData(cli)));

    public static HttpResponseMessage Response(byte[] content) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
}
