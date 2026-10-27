namespace PandaBear.Versioning.Tests;

/// <summary>
/// Locates the repository root for tests.
/// </summary>
internal static class RepositoryRoot
{
    /// <summary>
    /// Returns the first directory at or above the test base directory that contains SharedKernel.slnx, and throws when none does.
    /// </summary>
    public static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("SharedKernel.slnx was not found above " + AppContext.BaseDirectory);
    }
}
