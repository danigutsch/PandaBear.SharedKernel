namespace SharedKernel.Repositories;

/// <summary>Maps a language and features to the baseline configuration that <c>new</c> writes.</summary>
internal static class NewRepositoryCatalog
{
    /// <summary>Builds the baseline for the selected <paramref name="features"/>, in catalog order whatever order they were chosen in.</summary>
    public static BaselineConfiguration Build(string repositoryName, string defaultBranch, RepositoryLanguage language, IReadOnlyCollection<RepositoryFeature> features)
    {
        List<BaselineFileConfiguration> files = [];
        List<BaselineGitConfigurationEntry> settings = [];
        List<BaselineHookConfiguration> hooks = [];
        foreach (var feature in RepositoryFeature.All.Where(features.Contains))
        {
            if (feature == RepositoryFeature.Readme)
            {
                files.Add(new BaselineFileConfiguration("README.md", Profile: "readme"));
            }
            else if (feature == RepositoryFeature.Gitignore)
            {
                var profile = language switch
                {
                    _ when language == RepositoryLanguage.Dotnet => "gitignore-dotnet",
                    _ when language == RepositoryLanguage.Nix => "gitignore-nix",
                    _ => "gitignore"
                };
                files.Add(new BaselineFileConfiguration(".gitignore", Profile: profile));
            }
            else if (feature == RepositoryFeature.Gitattributes)
            {
                files.Add(new BaselineFileConfiguration(".gitattributes",
                    Profile: language == RepositoryLanguage.Shell ? "gitattributes-shell" : "gitattributes"));
            }
            else if (feature == RepositoryFeature.Editorconfig)
            {
                files.Add(new BaselineFileConfiguration(".editorconfig", Profile: "editorconfig"));
            }
            else if (feature == RepositoryFeature.Hooks)
            {
                hooks.Add(new BaselineHookConfiguration("pre-commit", Profile: "pre-commit-diff-check"));
            }
            else if (feature == RepositoryFeature.PrePushHook)
            {
                hooks.Add(new BaselineHookConfiguration("pre-push", Profile: "pre-push-diff-check"));
            }
            else if (feature == RepositoryFeature.CommitMessageHook)
            {
                hooks.Add(new BaselineHookConfiguration("commit-msg", Profile: "commit-msg-conventional"));
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
            else if (feature == RepositoryFeature.SdkBootstrap)
            {
                files.Add(new BaselineFileConfiguration("scripts/bootstrap-dotnet.sh", Profile: "sdk-bootstrap"));
            }
        }

        return new BaselineConfiguration(
            repositoryName,
            files.Count == 0 ? null : files,
            defaultBranch,
            settings.Count == 0 ? null : settings,
            hooks.Count == 0 ? null : new BaselineHooksConfiguration(hooks));
    }
}
