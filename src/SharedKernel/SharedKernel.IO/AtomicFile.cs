namespace PandaBear.IO;

/// <summary>Publishes file contents after a streaming write completes in the destination directory.</summary>
public static class AtomicFile
{
    private const string DefaultTemporaryFilePrefix = ".atomic-";

    /// <summary>Publishes a new file if the destination does not already exist.</summary>
    /// <param name="path">Destination in an existing directory.</param>
    /// <param name="write">Writes and optionally verifies the content. Leave the supplied stream open.</param>
    /// <param name="ct">Cancellation observed during the write and before publication.</param>
    /// <returns>Whether this call published the file.</returns>
    /// <remarks>On Unix, the destination filesystem must support hard links.</remarks>
    /// <exception cref="ArgumentException">The path is invalid.</exception>
    /// <exception cref="ArgumentNullException">The writer is null.</exception>
    /// <exception cref="IOException">The file cannot be written or the temporary file cannot be removed.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled before publication.</exception>
    public static ValueTask<bool> CreateIfAbsent(string path, Func<Stream, CancellationToken, ValueTask> write, CancellationToken ct = default)
        => Publish(path, write, DefaultTemporaryFilePrefix, overwrite: false, ct);

    /// <summary>Publishes a file, replacing any previous contents.</summary>
    /// <param name="path">Destination in an existing directory.</param>
    /// <param name="write">Writes and optionally verifies the content. Leave the supplied stream open.</param>
    /// <param name="ct">Cancellation observed during the write and before publication.</param>
    /// <exception cref="ArgumentException">The path is invalid.</exception>
    /// <exception cref="ArgumentNullException">The writer is null.</exception>
    /// <exception cref="IOException">The file cannot be written or the temporary file cannot be removed.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled before publication.</exception>
    public static ValueTask Replace(string path, Func<Stream, CancellationToken, ValueTask> write, CancellationToken ct)
        => Replace(path, DefaultTemporaryFilePrefix, write, ct);

    /// <summary>Publishes a replacement using a temporary name that callers can exclude from file inventory.</summary>
    /// <param name="path">Destination in an existing directory.</param>
    /// <param name="temporaryFilePrefix">
    /// A nonempty file name prefix containing only ASCII letters, digits, dots, hyphens, or underscores.
    /// The writer appends a 32-digit hexadecimal identifier and <c>.tmp</c>.
    /// </param>
    /// <param name="write">Writes and optionally verifies the content. Leave the supplied stream open.</param>
    /// <param name="ct">Cancellation observed during the write and before publication.</param>
    /// <exception cref="ArgumentException">The path or temporary file prefix is invalid.</exception>
    /// <exception cref="ArgumentNullException">The writer is null.</exception>
    /// <exception cref="IOException">The file cannot be written or the temporary file cannot be removed.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled before publication.</exception>
    public static async ValueTask Replace(
        string path,
        string temporaryFilePrefix,
        Func<Stream, CancellationToken, ValueTask> write,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(temporaryFilePrefix) ||
            temporaryFilePrefix.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_')))
        {
            throw new ArgumentException("The temporary file prefix must contain only ASCII letters, digits, dots, hyphens, or underscores.", nameof(temporaryFilePrefix));
        }

        await Publish(path, write, temporaryFilePrefix, overwrite: true, ct).ConfigureAwait(false);
    }

    private static async ValueTask<bool> Publish(
        string path, Func<Stream, CancellationToken, ValueTask> write, string temporaryFilePrefix, bool overwrite, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(write);
        ct.ThrowIfCancellationRequested();

        var destination = Path.GetFullPath(path);
        if (!overwrite && File.Exists(destination))
        {
            return false;
        }

        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $"{temporaryFilePrefix}{Guid.NewGuid():N}.tmp");
        var created = false;
        try
        {
            var stream = new FileStream(temporary, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            });
            created = true;
            await using (stream.ConfigureAwait(false))
            {
                await write(stream, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();
            if (overwrite)
            {
                MoveReplacement(temporary, destination, ct);
                created = false;
                return true;
            }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    File.Move(temporary, destination);
                    created = false;
                }
                else
                {
                    // Unix File.Move can race between its existence check and rename.
                    File.CreateHardLink(destination, temporary);
                }

                return true;
            }
            catch (IOException) when (File.Exists(destination))
            {
                return false;
            }
        }
        finally
        {
            if (created)
            {
                File.Delete(temporary);
            }
        }
    }

    private static void MoveReplacement(string temporary, string destination, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                // Preserve replacement with a delete-sharing reader already open.
                File.Replace(temporary, destination, destinationBackupFileName: null);
                return;
            }
            catch (FileNotFoundException)
            {
                // The destination did not exist or disappeared before replacement.
                ct.ThrowIfCancellationRequested();
            }
        }

        File.Move(temporary, destination, overwrite: true);
    }
}
