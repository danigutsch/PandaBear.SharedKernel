using PandaBear.IO;

namespace PandaBear.CI.Tool;

/// <summary>Reads small files whole, refusing one that is larger than its limit even if it grows while it is read.</summary>
internal static class BoundedFile
{
    /// <summary>Reads <paramref name="path"/> when it has at most <paramref name="maxBytes"/> bytes.</summary>
    /// <param name="path">The file.</param>
    /// <param name="maxBytes">The largest accepted size.</param>
    /// <param name="description">Names the file at the start of the size error.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <exception cref="InvalidDataException">The file is larger than <paramref name="maxBytes"/>.</exception>
    public static async Task<byte[]> Read(string path, int maxBytes, string description, CancellationToken ct) =>
        await BoundedReader.ReadFile(path, maxBytes, ct).ConfigureAwait(false)
        ?? throw new InvalidDataException($"{description} is larger than {DataSize.Format(maxBytes)}.");
}
