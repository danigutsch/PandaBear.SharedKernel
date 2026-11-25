using SharedKernel.Testing;

namespace SharedKernel.Mediator.GeneratorTests;

/// <summary>Helpers for locating the generator's AnalyzerReleases file in the repository.</summary>
internal static class GeneratorDiagnosticIdsTestsHelpers
{
    /// <summary>Walks up from the test output folder to find src/SharedKernel/SharedKernel.Mediator.SourceGenerator/AnalyzerReleases.Unshipped.md and returns its path, throwing when it is not found.</summary>
    public static string GetAnalyzerReleasesPath()
    {
        var relativePath = Path.Combine(
            "src",
            "SharedKernel",
            "SharedKernel.Mediator.SourceGenerator",
            "AnalyzerReleases.Unshipped.md");
        return Path.Combine(RepositoryRoot.Locate(relativePath), relativePath);
    }
}
