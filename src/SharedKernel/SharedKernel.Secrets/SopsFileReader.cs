using System.Text;
using PandaBear.IO;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>Reads the key holders of a SOPS file from disk, without a process and without decrypting.</summary>
public static class SopsFileReader
{
    private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Reads the file (YAML or JSON by its extension, at most 16 MiB) and returns the key holders that its metadata names.
    /// A missing file is a not-found result; an unreadable, empty, oversized, or unsupported file is an error.
    /// </summary>
    /// <param name="absoluteFile">The absolute path of the encrypted file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <param name="readTimeout">The longest the read may take; ten seconds by default. A FIFO or device that never ends is an error, not a hang.</param>
    /// <returns>The key holders or an error that never echoes file content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="absoluteFile"/> is <see langword="null"/>.</exception>
    public static async Task<Result<SopsFileRecipients>> ReadRecipients(string absoluteFile, CancellationToken cancellationToken, TimeSpan? readTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(absoluteFile);
        var invalid = ToolRuns.CheckFileArgument(absoluteFile);
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
            return Result.Error<SopsFileRecipients>("Only YAML and JSON SOPS files have readable recipients.");
        }

        string text;
        try
        {
            var bytes = await Task.Run(() => BoundedReader.ReadFile(absoluteFile, SopsFileLimits.MaxBytes, cancellationToken), cancellationToken).WaitAsync(readTimeout ?? DefaultReadTimeout, cancellationToken).ConfigureAwait(false);
            text = bytes is null ? string.Empty : Encoding.UTF8.GetString(bytes);
        }
        catch (TimeoutException)
        {
            return Result.Error<SopsFileRecipients>("The file did not finish reading (is it a FIFO or a device?).");
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Result.NotFound<SopsFileRecipients>("The file does not exist.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Error<SopsFileRecipients>("The file cannot be read.");
        }

        return text.Length == 0 ? Result.Error<SopsFileRecipients>("The file is empty or larger than 16 MiB.") : SopsMetadataReader.Read(text, format.Value);
    }
}
