namespace PandaBear.RepoConfig.Tests;

/// <summary>Test helpers for the Git settings and hooks of the <c>baseline</c> commands.</summary>
internal static class BaselineGitTestOperations
{
    /// <summary>Creates a Git repository whose consumer configuration is <paramref name="configuration"/>.</summary>
    public static async Task<TemporaryGitRepository> CreateRepository(string configuration)
    {
        var repository = await LinkTestRepository.CreateGit(seedCorpus: false, TestContext.Current.CancellationToken);
        repository.WriteText(".config/sharedkernel-repo.json", configuration);
        return repository;
    }

    /// <summary>Runs <c>baseline &lt;verb&gt;</c> against the repository.</summary>
    public static Task<TemporaryGitRepository.CommandResult> Baseline(TemporaryGitRepository repository, string verb, bool force = false) =>
        repository.RunCommand(force ? ["baseline", verb, "--root", repository.RootPath, "--force"] : ["baseline", verb, "--root", repository.RootPath], TestContext.Current.CancellationToken);

    /// <summary>Reads a local Git setting, or returns an empty string when it is not set.</summary>
    public static async Task<string> ConfigValue(TemporaryGitRepository repository, string key)
    {
        var result = await repository.RunGit(["config", "--local", "--get", key], TestContext.Current.CancellationToken);
        return result.StandardOutput.Trim();
    }
}
