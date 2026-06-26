namespace SharedKernel.Mediator.GeneratorTests;

internal static class GeneratorDiagnosticIdsTestsHelpers
{
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
