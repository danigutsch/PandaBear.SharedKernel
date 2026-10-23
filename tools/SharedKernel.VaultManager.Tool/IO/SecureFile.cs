using System.Text;

namespace PandaBear.VaultManager.Tool.IO;

/// <summary>Writes files that hold vault secrets.</summary>
internal static class SecureFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes text to <paramref name="path"/> readable and writable only by the current user on Unix. The text is
    /// written to a new temporary file in the same directory first and then moved into place, so an interrupted write
    /// never leaves a partial file and an overwritten file never keeps wider permissions. When the write does not complete,
    /// the temporary file is deleted.
    /// </summary>
    /// <param name="path">Destination file.</param>
    /// <param name="contents">Text to write as UTF-8 without a byte order mark.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="contents"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">
    /// The file exists and <paramref name="overwrite"/> is <see langword="false"/>, or it cannot be written, also when access
    /// to it or its directory is denied. When the temporary file cannot be deleted either, the message names it.
    /// </exception>
    public static void WriteAllText(string path, string contents, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(contents);

        WriteAllText(path, contents.AsSpan(), overwrite, CancellationToken.None);
    }

    /// <summary>
    /// Writes text like <see cref="WriteAllText(string, string, bool)"/> and observes a cancellation token before it creates
    /// the temporary file and again before it moves that file into place. A write canceled before it starts touches no
    /// file; a write canceled later deletes the temporary file. Either way <paramref name="path"/> stays unchanged.
    /// </summary>
    /// <param name="path">Destination file.</param>
    /// <param name="contents">Text to write as UTF-8 without a byte order mark.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <param name="ct">Observed before the temporary file is created and before it is moved into place.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="IOException">
    /// The file exists and <paramref name="overwrite"/> is <see langword="false"/>, or it cannot be written, also when access
    /// to it or its directory is denied. When the temporary file cannot be deleted either, the message names it.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    public static void WriteAllText(string path, ReadOnlySpan<char> contents, bool overwrite, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ct.ThrowIfCancellationRequested();

        string fullPath = Path.GetFullPath(path);
        if (!overwrite && File.Exists(fullPath))
        {
            throw new IOException($"Output file '{fullPath}' already exists. Choose another path or allow overwriting.");
        }

        string directory = Path.GetDirectoryName(fullPath)!;
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        bool created = false;
        try
        {
            using (FileStream stream = CreateOwnerOnly(temporaryPath))
            {
                created = true;
                using var writer = new StreamWriter(stream, Utf8);
                writer.Write(contents);
            }

            ct.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite);
        }
        catch (Exception exception)
        {
            // Whatever stopped the write, the temporary file it created must not outlive it.
            if (created)
            {
                DeleteTemporary(temporaryPath, fullPath, exception);
            }

            if (exception is UnauthorizedAccessException)
            {
                throw new IOException($"Output file '{fullPath}' cannot be written because access to it or its directory is denied.", exception);
            }

            throw;
        }
    }

    private static FileStream CreateOwnerOnly(string path)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(path, options);
    }

    // A temporary file that cannot be deleted is reported rather than left behind silently: it can hold the whole vault.
    private static void DeleteTemporary(string temporaryPath, string fullPath, Exception reason)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
            throw Undeletable(temporaryPath, fullPath, reason);
        }
        catch (UnauthorizedAccessException)
        {
            throw Undeletable(temporaryPath, fullPath, reason);
        }
    }

    private static IOException Undeletable(string temporaryPath, string fullPath, Exception reason) =>
        new($"Output file '{fullPath}' was not written, and its temporary file '{temporaryPath}' could not be deleted. Delete it by hand.", reason);
}
