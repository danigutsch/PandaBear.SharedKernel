
namespace PandaBear.RepoConfig.Tests;

/// <summary>Locates the repository root for the CI test selection tests.</summary>
internal static class CiTestSelectionTestProcess
{
    /// <summary>Returns the nearest ancestor of the test output directory that contains <c>SharedKernel.slnx</c>, or throws when none does.</summary>
    public static string GetRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
