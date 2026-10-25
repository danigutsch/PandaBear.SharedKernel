namespace PandaBear.RepoConfig.Tests;

internal static class GherkinTestRepository
{
    /// <summary>Returns a configuration whose <c>gherkin</c> section has the given patterns.</summary>
    public static string Configuration(string include = """["features/*"]""", string exclude = "[]", string allowedTagPatterns = "[]") =>
        $$"""
        {
          "version": 1,
          "gherkin": {
            "include": {{include}},
            "exclude": {{exclude}},
            "allowedTagPatterns": {{allowedTagPatterns}},
            "forbiddenTags": [],
            "featureNameMaxLength": 250,
            "scenarioNameMaxLength": 150
          }
        }
        """;

    /// <summary>Returns the non-empty lines of the standard error of a command.</summary>
    public static string[] ErrorLines(TemporaryGitRepository.CommandResult result) =>
        result.StandardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Creates a Git repository that holds the configuration and stages each file outside a <c>bin</c> directory.</summary>
    public static async Task<TemporaryGitRepository> Create(IReadOnlyDictionary<string, string> files, string configuration)
    {
        var repository = new TemporaryGitRepository();
        try
        {
            await repository.Initialize(TestContext.Current.CancellationToken);
            repository.WriteText(".config/sharedkernel-repo.json", configuration);
            foreach (var (path, content) in files)
            {
                repository.WriteText(path, content);

                // A build output directory stays untracked, as a .gitignore entry keeps it.
                if (!path.Contains("/bin/", StringComparison.Ordinal))
                {
                    await repository.Stage(path, TestContext.Current.CancellationToken);
                }
            }

            return repository;
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }
}
