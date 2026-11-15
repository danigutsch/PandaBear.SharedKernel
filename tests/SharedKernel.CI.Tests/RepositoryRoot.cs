namespace PandaBear.CI.Tests;

/// <summary>Finds the root of this repository from the test assembly.</summary>
internal static class RepositoryRoot
{
    /// <summary>Returns the nearest ancestor of the test output directory that holds <c>SharedKernel.slnx</c>.</summary>
    /// <exception cref="InvalidOperationException">No ancestor holds the solution.</exception>
    public static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository root holding SharedKernel.slnx was not found.");
    }
}
