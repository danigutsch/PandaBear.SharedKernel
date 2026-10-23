namespace PandaBear.Documentation.Tests;

internal static class RepositoryFiles
{
    public static string GetRootPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    /// <summary>
    /// Returns the text after each occurrence of <paramref name="invocation" />, up to the end of the line or
    /// inline code span, in file order.
    /// </summary>
    public static IReadOnlyList<string> FindToolArguments(string relativePath, string invocation)
    {
        ArgumentException.ThrowIfNullOrEmpty(invocation);

        var text = File.ReadAllText(Path.Combine(GetRootPath(), relativePath));
        var arguments = new List<string>();
        var index = text.IndexOf(invocation, StringComparison.Ordinal);
        while (index >= 0)
        {
            var start = index + invocation.Length;
            var length = text.AsSpan(start).IndexOfAny('\n', '`');
            arguments.Add(length < 0 ? text[start..] : text.Substring(start, length));
            index = text.IndexOf(invocation, start, StringComparison.Ordinal);
        }

        return arguments;
    }
}
