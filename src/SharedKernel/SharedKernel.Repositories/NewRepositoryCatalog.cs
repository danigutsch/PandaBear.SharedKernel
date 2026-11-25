namespace SharedKernel.Repositories;

/// <summary>Maps a language and features to the baseline configuration that <c>new</c> writes.</summary>
internal static class NewRepositoryCatalog
{
    /// <summary>Builds the baseline for the selected <paramref name="features"/>, in catalog order whatever order they were chosen in.</summary>
    public static BaselineConfiguration Build(string repositoryName, string defaultBranch, RepositoryLanguage language, IReadOnlyCollection<RepositoryFeature> features)
    {
        List<BaselineFileConfiguration> files = [];
        List<BaselineGitConfigurationEntry> settings = [];
        BaselineHooksConfiguration? hooks = null;
        foreach (var feature in RepositoryFeature.All.Where(features.Contains))
        {
            if (feature == RepositoryFeature.Readme)
            {
                files.Add(new BaselineFileConfiguration("README.md", Profile: "readme"));
            }
            else if (feature == RepositoryFeature.Gitignore)
            {
                files.Add(new BaselineFileConfiguration(".gitignore", Profile: language == RepositoryLanguage.Dotnet ? "gitignore-dotnet" : "gitignore"));
            }
            else if (feature == RepositoryFeature.Gitattributes)
            {
                files.Add(new BaselineFileConfiguration(".gitattributes", Profile: "gitattributes"));
            }
            else if (feature == RepositoryFeature.Editorconfig)
            {
                files.Add(new BaselineFileConfiguration(".editorconfig", Profile: "editorconfig"));
            }
            else if (feature == RepositoryFeature.Hooks)
            {
                hooks = new BaselineHooksConfiguration([new BaselineHookConfiguration("pre-commit", Profile: "pre-commit-diff-check")]);
            }
            else if (feature == RepositoryFeature.GitConfig)
            {
                settings.AddRange(
                [
                    new BaselineGitConfigurationEntry("init.defaultBranch", defaultBranch),
                    new BaselineGitConfigurationEntry("pull.ff", "only"),
                    new BaselineGitConfigurationEntry("fetch.prune", "true"),
                ]);
            }
            else if (feature == RepositoryFeature.SignedCommits)
            {
                settings.AddRange(
                [
                    new BaselineGitConfigurationEntry("commit.gpgsign", "true"),
                    new BaselineGitConfigurationEntry("tag.gpgsign", "true"),
                    new BaselineGitConfigurationEntry("gpg.format", "ssh"),
                ]);
            }
        }

        return new BaselineConfiguration(
            repositoryName,
            files.Count == 0 ? null : files,
            defaultBranch,
            settings.Count == 0 ? null : settings,
            hooks);
    }
}
