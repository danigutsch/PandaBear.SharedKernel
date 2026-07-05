namespace SharedKernel.Testing.Snapshots;

/// <summary>
/// Normalizes text before snapshot or approval comparison.
/// </summary>
public static class SnapshotText
{
    /// <summary>
    /// Normalizes line endings to LF and trims trailing whitespace from each line.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text.</returns>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        return string.Join('\n', lines.Select(line => line.TrimEnd()));
    }
}
