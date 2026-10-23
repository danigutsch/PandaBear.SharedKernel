using System.Text;

namespace PandaBear.VaultManager.Tool.IO;

/// <summary>Writes files that hold vault secrets.</summary>
internal static class SecureFile
{
    /// <summary>
    /// Writes text to <paramref name="path"/> readable and writable only by the current user on Unix. The text is
    /// written to a new temporary file in the same directory first and then moved into place, so an interrupted write
    /// never leaves a partial file and an overwritten file never keeps wider permissions.
    /// </summary>
    /// <param name="path">Destination file.</param>
    /// <param name="contents">Text to write as UTF-8 without a byte order mark.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <exception cref="IOException">
    /// The file exists and <paramref name="overwrite"/> is <see langword="false"/>, or it cannot be written, also when access
    /// to it or its directory is denied.
    /// </exception>
    public static void WriteAllText(string path, string contents, bool overwrite)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(contents);

        string fullPath = Path.GetFullPath(path);
        if (!overwrite && File.Exists(fullPath))
        {
            throw new IOException($"Output file '{fullPath}' already exists. Choose another path or allow overwriting.");
        }

        string directory = Path.GetDirectoryName(fullPath)!;
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
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

        bool created = false;
        try
        {
            using (var stream = new FileStream(temporaryPath, options))
            {
                created = true;
                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(contents);
            }

            File.Move(temporaryPath, fullPath, overwrite);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new IOException($"Output file '{fullPath}' cannot be written because access to it or its directory is denied.", exception);
        }
        finally
        {
            // Only a created file can be left behind. Deleting in a directory that may not be searched would fail and
            // replace the error above.
            if (created)
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
