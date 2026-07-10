namespace SharedKernel.Versioning;

/// <summary>
/// Reads commit-message streams used by release calculation commands.
/// </summary>
public static class CommitMessageInput
{
    /// <summary>
    /// Reads newline-separated or null-separated commit messages.
    /// </summary>
    /// <param name="reader">The input reader.</param>
    /// <returns>The parsed commit messages.</returns>
    public static async Task<IReadOnlyList<string>> ReadMessages(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var input = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var separator = input.Contains('\0', StringComparison.Ordinal) ? '\0' : '\n';
        return input.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
