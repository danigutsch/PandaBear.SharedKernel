using System.Text;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>Reads the key holders of a SOPS file from disk, without a process and without decrypting.</summary>
public static class SopsFileReader
{
    private const int MaxFileBytes = 16 * 1024 * 1024;

    /// <summary>
    /// Reads the file (YAML or JSON by its extension, at most 16 MiB) and returns the key holders that its metadata names.
    /// A missing file is a not-found result; an unreadable, empty, oversized, or unsupported file is an error.
    /// </summary>
    /// <param name="absoluteFile">The absolute path of the encrypted file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The key holders or an error that never echoes file content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="absoluteFile"/> is <see langword="null"/>.</exception>
    public static async Task<Result<SopsFileRecipients>> ReadRecipients(string absoluteFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(absoluteFile);
        var invalid = ToolInvocation.CheckFile(absoluteFile);
        if (invalid is not null)
        {
            return Result.Invalid<SopsFileRecipients>(invalid, "file", invalid);
        }

        var extension = Path.GetExtension(absoluteFile);
        SopsFormat? format = extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            ? SopsFormat.Yaml
            : extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ? SopsFormat.Json : null;
        if (format is null)
        {
            return ToolInvocation.Error<SopsFileRecipients>("Only YAML and JSON SOPS files have readable recipients.");
        }

        string text;
        try
        {
            text = await ReadBounded(absoluteFile, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Result.NotFound<SopsFileRecipients>("The file does not exist.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ToolInvocation.Error<SopsFileRecipients>("The file cannot be read.");
        }

        return text.Length == 0 ? ToolInvocation.Error<SopsFileRecipients>("The file is empty or larger than 16 MiB.") : SopsMetadataReader.Read(text, format.Value);
    }

    private static async Task<string> ReadBounded(string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[MaxFileBytes + 1];
            var length = 0;
            int read;
            while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                length += read;
            }

            return length > MaxFileBytes ? string.Empty : Encoding.UTF8.GetString(buffer.AsSpan(0, length));
        }
    }
}
