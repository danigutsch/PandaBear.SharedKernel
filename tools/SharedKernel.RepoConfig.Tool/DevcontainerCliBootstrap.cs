using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Downloads the pinned Node.js runtime and Dev Container CLI package, verifies their hashes before reading them,
/// and extracts only <c>bin/node</c> and the CLI package into a cache directory.
/// </summary>
internal static class DevcontainerCliBootstrap
{
    private const long MaxArchiveBytes = 256L * 1024 * 1024;
    private const string IntegrityPrefix = "sha512-";

    /// <summary>
    /// Returns the cached CLI under <paramref name="toolsDirectory"/>, downloading and extracting it first when the
    /// cache for <paramref name="pins"/> does not exist.
    /// </summary>
    /// <exception cref="InvalidOperationException">A download fails, a hash does not match, or an archive is unsafe or incomplete.</exception>
    public static async Task<DevcontainerCli> Ensure(string toolsDirectory, DevcontainerCliPins pins, HttpClient httpClient, CancellationToken ct)
    {
        var prefix = Path.Combine(toolsDirectory, $"devcontainers-cli-{pins.CliVersion}-node-{pins.NodeVersion}");
        DevcontainerCli cli = new(
            Path.Combine(prefix, "node", "v" + pins.NodeVersion, "bin", "node"),
            Path.Combine(prefix, "cli", pins.CliVersion, "package", "devcontainer.js"));

        // The cache directory appears only through the move below, so an existing one is complete.
        if (Directory.Exists(prefix))
        {
            return cli;
        }

        var download = Directory.CreateTempSubdirectory("sharedkernel-devcontainer-");
        string? staging = null;
        try
        {
            var nodeArchive = Path.Combine(download.FullName, "node.tar.gz");
            var nodeHash = await Download(httpClient, pins.NodeArchive, nodeArchive, HashAlgorithmName.SHA256, ct).ConfigureAwait(false);
            if (!nodeHash.AsSpan().SequenceEqual(Convert.FromHexString(pins.NodeSha256)))
            {
                throw new InvalidOperationException($"Node.js archive {pins.NodeArchive} does not match its pinned SHA-256.");
            }

            var cliArchive = Path.Combine(download.FullName, "cli.tgz");
            var cliHash = await Download(httpClient, pins.CliArchive, cliArchive, HashAlgorithmName.SHA512, ct).ConfigureAwait(false);
            if (!pins.CliIntegrity.StartsWith(IntegrityPrefix, StringComparison.Ordinal)
                || !cliHash.AsSpan().SequenceEqual(Convert.FromBase64String(pins.CliIntegrity[IntegrityPrefix.Length..])))
            {
                throw new InvalidOperationException($"Dev Container CLI archive {pins.CliArchive} does not match its pinned npm integrity.");
            }

            Directory.CreateDirectory(toolsDirectory);
            staging = Path.Combine(toolsDirectory, ".staging-" + Guid.NewGuid().ToString("N"));
            await ExtractNode(
                nodeArchive,
                $"node-v{pins.NodeVersion}-linux-x64/bin/node",
                Path.Combine(staging, Path.GetRelativePath(prefix, cli.Executable)),
                ct).ConfigureAwait(false);
            var cliRoot = Path.Combine(staging, "cli", pins.CliVersion);
            await ExtractPackage(cliArchive, cliRoot, ct).ConfigureAwait(false);
            if (!File.Exists(Path.Combine(cliRoot, "package", "devcontainer.js")))
            {
                throw new InvalidOperationException("Dev Container CLI archive has no package/devcontainer.js.");
            }

            try
            {
                Directory.Move(staging, prefix);
                staging = null;
            }
            catch (IOException) when (Directory.Exists(prefix))
            {
                // A concurrent run published the same pins first; its cache is equivalent.
            }

            return cli;
        }
        finally
        {
            download.Delete(recursive: true);
            if (staging is not null && Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    private static async Task<byte[]> Download(HttpClient httpClient, Uri uri, string path, HashAlgorithmName algorithm, CancellationToken ct)
    {
        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Downloading {uri} failed with HTTP status {(int)response.StatusCode}.");
            }

            using var hash = IncrementalHash.CreateHash(algorithm);
            var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using (destination.ConfigureAwait(false))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                    {
                        total += read;
                        if (total > MaxArchiveBytes)
                        {
                            throw new InvalidOperationException($"{uri} is larger than {MaxArchiveBytes} bytes.");
                        }

                        hash.AppendData(buffer, 0, read);
                        await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    }
                }
            }

            return hash.GetHashAndReset();
        }
        catch (HttpRequestException exception)
        {
            // The transport message can carry request details, so it is not echoed.
            throw new InvalidOperationException($"Downloading {uri} failed.", exception);
        }
    }

    private static async Task ExtractNode(string archive, string entryName, string destination, CancellationToken ct)
    {
        var found = false;
        await ReadEntries(archive, async entry =>
        {
            if (!string.Equals(entry.Name, entryName, StringComparison.Ordinal))
            {
                return;
            }

            if (found || !IsRegularFile(entry))
            {
                throw new InvalidOperationException($"Node.js archive entry {entryName} is not a single regular file.");
            }

            found = true;
            await WriteFile(entry, destination, ct).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    destination,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }, ct).ConfigureAwait(false);

        if (!found)
        {
            throw new InvalidOperationException($"Node.js archive has no {entryName}.");
        }
    }

    private static Task ExtractPackage(string archive, string root, CancellationToken ct) =>
        ReadEntries(archive, async entry =>
        {
            if (entry.EntryType is TarEntryType.GlobalExtendedAttributes)
            {
                return;
            }

            var isDirectory = entry.EntryType is TarEntryType.Directory;
            var name = isDirectory && entry.Name.EndsWith('/') ? entry.Name[..^1] : entry.Name;
            if (!IsPackagePath(name, isDirectory))
            {
                throw new InvalidOperationException($"Dev Container CLI archive entry '{entry.Name}' has an unsafe path.");
            }

            if (isDirectory)
            {
                return;
            }

            if (!IsRegularFile(entry))
            {
                throw new InvalidOperationException($"Dev Container CLI archive entry '{entry.Name}' is not a regular file.");
            }

            await WriteFile(entry, Path.Combine(root, name), ct).ConfigureAwait(false);
        }, ct);

    private static async Task ReadEntries(string archive, Func<TarEntry, Task> read, CancellationToken ct)
    {
        var file = File.OpenRead(archive);
        await using (file.ConfigureAwait(false))
        {
            var gzip = new GZipStream(file, CompressionMode.Decompress);
            await using (gzip.ConfigureAwait(false))
            {
                var reader = new TarReader(gzip);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.GetNextEntryAsync(copyData: false, ct).ConfigureAwait(false) is { } entry)
                    {
                        await read(entry).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private static async Task WriteFile(TarEntry entry, string path, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using (destination.ConfigureAwait(false))
        {
            if (entry.DataStream is { } data)
            {
                await data.CopyToAsync(destination, ct).ConfigureAwait(false);
            }
        }
    }

    private static bool IsRegularFile(TarEntry entry) =>
        entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile;

    // Extraction writes only below package/: every segment is a plain name, so no entry can leave the cache directory.
    private static bool IsPackagePath(string name, bool isDirectory)
    {
        var segments = name.Split('/');
        return (segments.Length > 1 || isDirectory)
            && segments[0] == "package"
            && segments.All(static segment => segment.Length > 0
                && segment is not ("." or "..")
                && segment.IndexOfAny(['\\', ':', '\0']) < 0);
    }
}
