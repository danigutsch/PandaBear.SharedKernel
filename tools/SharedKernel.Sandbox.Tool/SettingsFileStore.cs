using System.Text;
using PandaBear.Tooling;

namespace PandaBear.Sandbox.Tool;

/// <summary>Reads and atomically replaces the settings file under the root. Never follows a symbolic link and never leaves the root.</summary>
internal static class SettingsFileStore
{
    /// <summary>The largest settings file the tool reads or writes.</summary>
    public const int MaxBytes = 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Resolves <paramref name="relativePath"/> under <paramref name="root"/>.</summary>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The path is not repository-relative, is not a <c>.json</c> file, or is or traverses a symbolic link.</exception>
    public static string Resolve(string root, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(relativePath);
        if (!relativePath.EndsWith(".json", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{SandboxConfigurationFile.RelativePath}: $.settings.target must name a .json file.");
        }

        try
        {
            return RepositoryPath.Resolve(root, relativePath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{relativePath}: {exception.Message}", exception);
        }
    }

    /// <summary>Reads the file as strict UTF-8.</summary>
    /// <returns>The text, or <see langword="null"/> when the file does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The file is larger than 1 MiB or not valid UTF-8.</exception>
    public static async Task<string?> Read(string path, string displayName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            return null;
        }

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[MaxBytes + 1];
            var length = 0;
            int read;
            while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                length += read;
            }

            if (length > MaxBytes)
            {
                throw new InvalidOperationException($"{displayName} is larger than 1 MiB.");
            }

            try
            {
                var skip = length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF ? 3 : 0;
                return StrictUtf8.GetString(buffer, skip, length - skip);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidOperationException($"{displayName} is not valid UTF-8.", exception);
            }
        }
    }

    /// <summary>
    /// Replaces the file: writes a temporary sibling, flushes it, and renames it over the file, after checking that the file
    /// still has <paramref name="expected"/>. A failure leaves the original untouched and removes the temporary file and a
    /// directory this call created.
    /// </summary>
    /// <param name="root">The repository root; the path must stay inside it.</param>
    /// <param name="relativePath">The settings file relative to <paramref name="root"/>.</param>
    /// <param name="content">The new content.</param>
    /// <param name="expected">The content the file must have now, or <see langword="null"/> when it must not exist.</param>
    /// <param name="beforeCommit">Runs after the temporary file is complete and before the rename; for tests of the failure path.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The file changed since it was read, the content is larger than 1 MiB, or the path is a symbolic link.</exception>
    public static void Write(string root, string relativePath, string content, string? expected, Action? beforeCommit = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = Resolve(root, relativePath);
        var bytes = StrictUtf8.GetBytes(content);
        if (bytes.Length > MaxBytes)
        {
            throw new InvalidOperationException($"{relativePath} would be larger than 1 MiB.");
        }

        var directory = Path.GetDirectoryName(path)!;
        List<string> createdDirectories = [];
        for (var missing = directory; !Directory.Exists(missing); missing = Path.GetDirectoryName(missing)!)
        {
            createdDirectories.Add(missing);
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (createdDirectories.Count > 0)
            {
                Directory.CreateDirectory(directory);
            }

            VerifyUnchanged(path, relativePath, expected);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (expected is not null && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(path));
            }

            beforeCommit?.Invoke();
            VerifyUnchanged(path, relativePath, expected);
            File.Move(temporary, path, overwrite: expected is not null);
            FlushDirectory(directory);
        }
        catch
        {
            File.Delete(temporary);
            foreach (var created in createdDirectories.Where(Directory.Exists).Where(static created => !Directory.EnumerateFileSystemEntries(created).Any()))
            {
                Directory.Delete(created);
            }

            throw;
        }
    }

    // Makes the rename durable where the platform lets a directory be opened; elsewhere the rename is still atomic.
    private static void FlushDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var handle = File.OpenHandle(directory, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            RandomAccess.FlushToDisk(handle);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Durability only: the write has already succeeded and a refusal to open a directory says nothing about security.
        }
    }

    private static void VerifyUnchanged(string path, string relativePath, string? expected)
    {
        var unchanged = expected is null
            ? !File.Exists(path)
            : File.Exists(path) && string.Equals(File.ReadAllText(path, StrictUtf8), expected, StringComparison.Ordinal);
        if (!unchanged)
        {
            throw new InvalidOperationException($"{relativePath} changed after the plan was made; nothing was written. Run the command again.");
        }
    }
}
