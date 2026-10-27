namespace PandaBear.Documentation.Tests;

/// <summary>Locates the repository root and reads tool invocations out of repository files.</summary>
internal static class RepositoryFiles
{
    /// <summary>Walks up from the test assembly directory to the first directory containing <c>SharedKernel.slnx</c>.</summary>
    /// <returns>The repository root path.</returns>
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

    private const string LocalToolRun = "dotnet tool run ";

    /// <summary>
    /// Returns the text after each occurrence of <paramref name="invocation" />, up to the end of the line or
    /// inline code span, in file order. An occurrence that follows <c>dotnet tool run</c> belongs to a local-tool
    /// invocation, which callers find by passing an invocation that starts with <c>dotnet tool run</c>.
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
            if (!text.AsSpan(0, index).EndsWith(LocalToolRun, StringComparison.Ordinal))
            {
                arguments.Add(length < 0 ? text[start..] : text.Substring(start, length));
            }

            index = text.IndexOf(invocation, start, StringComparison.Ordinal);
        }

        return arguments;
    }
}
