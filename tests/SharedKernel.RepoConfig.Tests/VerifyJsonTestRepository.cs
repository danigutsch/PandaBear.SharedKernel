namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Factory for the Git repository used by JSON verification tests.
/// </summary>
internal static class VerifyJsonTestRepository
{
    /// <summary>Creates a Git repository whose untracked package cache holds JSON that verification must not read.</summary>
    public static async Task<TemporaryGitRepository> Create(CancellationToken cancellationToken)
    {
        var repository = new TemporaryGitRepository();
        try
        {
            await repository.Initialize(cancellationToken);
            Directory.CreateDirectory(Path.Combine(repository.RootPath, ".nuget", "packages", "microsoft.extensions.configuration.json"));
            repository.WriteText(".nuget/packages/cache.json", "not valid JSON");
            return repository;
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }
}
