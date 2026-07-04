namespace SharedKernel.Versioning.Tool;

internal static class CommitMessageInput
{
    public static async Task<IReadOnlyList<string>> ReadMessages(TextReader reader)
    {
        var input = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var separator = input.Contains('\0', StringComparison.Ordinal) ? '\0' : '\n';
        return input.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
