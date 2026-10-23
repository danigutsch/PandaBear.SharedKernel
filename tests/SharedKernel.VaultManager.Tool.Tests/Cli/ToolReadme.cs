namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Reads the tool README from the repository the tests were built from.</summary>
internal static class ToolReadme
{
    private const string Invocation = "sharedkernel-vault ";

    /// <summary>
    /// Returns the arguments of every <c>sharedkernel-vault</c> command line in the README, in file order. A command line
    /// ends at the end of its line or of its inline code span.
    /// </summary>
    public static IReadOnlyList<string[]> CommandLines()
    {
        string text = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "SharedKernel.VaultManager.Tool", "README.md"));
        var commandLines = new List<string[]>();
        int index = text.IndexOf(Invocation, StringComparison.Ordinal);
        while (index >= 0)
        {
            int start = index + Invocation.Length;
            int length = text.AsSpan(start).IndexOfAny('\n', '`');
            string arguments = length < 0 ? text[start..] : text.Substring(start, length);
            commandLines.Add(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            index = text.IndexOf(Invocation, start, StringComparison.Ordinal);
        }

        return commandLines;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found above the test output directory.");
    }
}
