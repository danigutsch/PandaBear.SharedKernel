namespace PandaBear.Mediator.GeneratorTests;

/// <summary>Helpers for locating the generator's AnalyzerReleases file in the repository.</summary>
internal static class GeneratorDiagnosticIdsTestsHelpers
{
    /// <summary>Walks up from the test output folder to find src/SharedKernel/SharedKernel.Mediator.SourceGenerator/AnalyzerReleases.Unshipped.md and returns its path, throwing when it is not found.</summary>
    public static string GetAnalyzerReleasesPath()
    {
        return FindRepositoryPathContaining(
            Path.Combine(
                "src",
                "SharedKernel",
                "SharedKernel.Mediator.SourceGenerator",
                "AnalyzerReleases.Unshipped.md"));
    }

    private static string FindRepositoryPathContaining(string relativePath)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var candidatePath = Path.Combine(currentDirectory.FullName, relativePath);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository path for '{relativePath}'.");
    }
}
