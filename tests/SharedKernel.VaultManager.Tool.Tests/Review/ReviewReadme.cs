namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Reads the tool README from the repository the tests were built from.</summary>
internal static class ReviewReadme
{
    /// <summary>Returns the text of the tool README, found by walking up from the test output directory to the repository root.</summary>
    public static string Text()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName ?? throw new InvalidOperationException("The repository root was not found above the test output directory.");
        return File.ReadAllText(Path.Combine(root, "tools", "SharedKernel.VaultManager.Tool", "README.md"));
    }
}
