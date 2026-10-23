using PandaBear.VaultManager.Tool.Attachments;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Opens each input archive once, after the inputs were read, so attachment text can be scanned.</summary>
internal sealed class InputArchives : IDisposable
{
    private readonly Dictionary<string, SafeArchive?> archives = new(StringComparer.Ordinal);

    /// <summary>Opens and validates an input archive, or returns the one opened before.</summary>
    /// <param name="inputPath">Input file.</param>
    /// <param name="warnings">Receives one warning per file that cannot be opened.</param>
    /// <returns>The archive, or <see langword="null"/> when it cannot be opened.</returns>
    public SafeArchive? Open(string inputPath, ICollection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        ArgumentNullException.ThrowIfNull(warnings);

        if (archives.TryGetValue(inputPath, out SafeArchive? archive))
        {
            return archive;
        }

        try
        {
            archive = SafeArchive.Open(inputPath);
        }
        catch (InvalidDataException exception)
        {
            warnings.Add(NotOpened(inputPath, exception));
        }
        catch (IOException exception)
        {
            warnings.Add(NotOpened(inputPath, exception));
        }
        catch (UnauthorizedAccessException exception)
        {
            warnings.Add(NotOpened(inputPath, exception));
        }

        archives.Add(inputPath, archive);
        return archive;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (SafeArchive? archive in archives.Values)
        {
            archive?.Dispose();
        }

        archives.Clear();
    }

    private static string NotOpened(string inputPath, Exception exception) =>
        $"{Path.GetFileName(inputPath)}: attachments were not scanned because the archive could not be opened again ({exception.Message})";
}
