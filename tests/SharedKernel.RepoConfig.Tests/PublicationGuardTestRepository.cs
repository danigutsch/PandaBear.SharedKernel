namespace SharedKernel.RepoConfig.Tests;

/// <summary>Creates an isolated staged policy for publication guard tests.</summary>
internal static class PublicationGuardTestRepository
{
    /// <summary>The repository-relative path of the staged policy.</summary>
    public const string PolicyPath = ".config/publication-guard.json";

    /// <summary>The repository-relative path of the ignored local patterns file.</summary>
    public const string LocalPatternsPath = ".config/publication-guard.local.patterns";

    /// <summary>A synthetic string used to verify pattern matches and redaction.</summary>
    public const string Pattern = "synthetic-private-marker";

    /// <summary>A synthetic policy with one path rule and one pattern rule.</summary>
    public const string Policy = """
        {
          "version": 1,
          "neverTrack": [":(top,glob)private/**"],
          "patterns": [{"id": "marker", "regex": "synthetic-private-marker"}],
          "allowlist": []
        }
        """;

    /// <summary>Creates a repository with a staged policy and leaves the working tree available for mutation.</summary>
    public static async Task<TemporaryGitRepository> Create(string policy, CancellationToken ct)
    {
        var repository = new TemporaryGitRepository();
        try
        {
            await repository.Initialize(ct);
            await Stage(repository, PolicyPath, policy, ct);
            return repository;
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    /// <summary>Writes and stages a file by its exact path.</summary>
    public static async Task Stage(TemporaryGitRepository repository, string path, string content, CancellationToken ct)
    {
        repository.WriteText(path, content);
        await repository.Stage(path, ct);
    }
}
