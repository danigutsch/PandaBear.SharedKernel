using System.Globalization;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Implements <c>new</c>: starts a repository from a language and features by planning a baseline in memory, writing
/// it and its consumer configuration in one rollback batch, and initializing Git.
/// </summary>
internal static class NewCommand
{
    /// <summary>
    /// Previews the new repository, or with <paramref name="force"/> writes it. Any problem, including a conflict with an
    /// existing file or Git setting, writes nothing and returns 1, in a preview too.
    /// </summary>
    public static async Task<int> Run(
        string rootPath,
        string? name,
        string defaultBranch,
        string language,
        string? sdkVersion,
        IReadOnlyList<string> featureIds,
        bool force,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootPath);
        if (!RepoLanguage.TryParse(language, out var repoLanguage))
        {
            await error.WriteLineAsync($"unknown language '{ControlCharacters.Escape(language)}'; the languages are: {string.Join(", ", RepoLanguage.All.Select(static l => l.Id))}.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }

        List<RepoFeature> features = [];
        foreach (var id in featureIds)
        {
            if (!RepoFeature.TryParse(id, out var feature))
            {
                await error.WriteLineAsync($"unknown feature '{ControlCharacters.Escape(id)}'; the features are: {string.Join(", ", RepoFeature.All.Select(static f => f.Id))}.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 1;
            }

            if (feature.RequiredLanguage is { } required && required != repoLanguage)
            {
                await error.WriteLineAsync($"feature '{feature.Id}' needs --language {required.Id}.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 1;
            }

            if (!features.Contains(feature))
            {
                features.Add(feature);
            }
        }

        if (features.Count == 0)
        {
            features.AddRange(RepoFeature.DefaultsFor(repoLanguage));
        }

        SdkVersion? sdk = null;
        if (sdkVersion is not null && !SdkVersion.TryParse(sdkVersion, out sdk))
        {
            await error.WriteLineAsync($"invalid --sdk-version '{ControlCharacters.Escape(sdkVersion)}'; use major.minor.patch, such as 10.0.100, with a major version of 6 or higher.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }

        if (features.FirstOrDefault(static f => f.NeedsSdkVersion) is { } needsSdk && sdk is null)
        {
            await error.WriteLineAsync($"feature '{needsSdk.Id}' needs --sdk-version, such as --sdk-version 10.0.100.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }

        var repositoryName = name ?? Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var baseline = NewRepositoryCatalog.Build(repositoryName, defaultBranch, repoLanguage, features);
        if (baseline.FindError() is { } problem)
        {
            // The shared validation names JSON paths; this command takes options.
            var message = problem
                .Replace("$.baseline.repositoryName", "--name (the repository name)", StringComparison.Ordinal)
                .Replace("$.baseline.defaultBranch", "--branch", StringComparison.Ordinal);
            await error.WriteLineAsync(message.AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }

        var configurationPath = Path.Combine(root, ConsumerConfigurationFile.RelativePath);
        if (File.Exists(configurationPath))
        {
            await error.WriteLineAsync($"{ConsumerConfigurationFile.RelativePath} already exists; use baseline apply for an existing repository.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }

        var entries = (await BaselinePlanner.Plan(root, baseline, cancellationToken).ConfigureAwait(false)).ToList();
        var gitPath = Path.Combine(root, ".git");
        var hasRepository = Directory.Exists(gitPath) || File.Exists(gitPath);
        var settings = baseline.GitSettings.Count == 0
            ? []
            : hasRepository
                ? await BaselineGit.Plan(root, baseline.GitSettings, cancellationToken).ConfigureAwait(false)
                : [.. baseline.GitSettings.Select(static s => new BaselineGitEntry(s.Key, s.Value, BaselineStatus.Missing, s.OverwriteExisting))];

        var conflicts = entries.Where(static e => e.IsConflict).ToArray();
        var settingConflicts = settings.Where(static e => e.IsConflict).ToArray();
        foreach (var conflict in conflicts)
        {
            var reason = conflict.Status == BaselineStatus.Modified ? "was edited after the baseline wrote it" : "exists without the baseline marker";
            await error.WriteLineAsync($"conflict: {ControlCharacters.Escape(conflict.Path)} {reason}.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var conflict in settingConflicts)
        {
            await error.WriteLineAsync($"conflict: git config {conflict.Key} is set to a different value.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (conflicts.Length + settingConflicts.Length > 0)
        {
            return 1;
        }

        foreach (var (seedPath, content) in NewRepositorySeeds.Render(repositoryName, features, sdk))
        {
            var fullPath = RepositoryPath.Resolve(root, seedPath);
            var exists = File.Exists(fullPath);
            if (exists)
            {
                await error.WriteLineAsync($"conflict: {seedPath} exists; new writes it only when it is missing.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 1;
            }

            entries.Add(new BaselineEntry(seedPath, fullPath, BaselineStatus.Missing, content, null, false));
        }

        var configuration = SerializeConfiguration(baseline);
        entries.Add(new BaselineEntry(ConsumerConfigurationFile.RelativePath, configurationPath, BaselineStatus.Missing, configuration, null, false));
        var changes = entries.Where(static e => e.Status != BaselineStatus.Current).ToArray();
        var settingChanges = settings.Where(static e => e.Status != BaselineStatus.Current).ToArray();

        if (force)
        {
            Directory.CreateDirectory(root);
            BaselineCommand.Write(root, changes);
            if (!hasRepository)
            {
                await BaselineGit.Init(root, defaultBranch, cancellationToken).ConfigureAwait(false);
            }

            foreach (var change in settingChanges)
            {
                await BaselineGit.Set(root, change, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var change in changes)
        {
            await output.WriteLineAsync((force ? $"Created {ControlCharacters.Escape(change.Path)}." : $"preview: would create {ControlCharacters.Escape(change.Path)}.").AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (!hasRepository)
        {
            await output.WriteLineAsync((force ? $"Ran git init -b {defaultBranch}." : $"preview: would run git init -b {defaultBranch}.").AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var change in settingChanges)
        {
            await output.WriteLineAsync((force ? $"Set git config {change.Key}." : $"preview: would set git config {change.Key}.").AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await output.WriteLineAsync(
            (force
                ? string.Create(CultureInfo.InvariantCulture, $"{repositoryName}: {changes.Length} files created and {settingChanges.Length} git settings set.")
                : string.Create(CultureInfo.InvariantCulture, $"preview: {changes.Length} files and {settingChanges.Length} git settings would be created; --force writes them.")).AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static string SerializeConfiguration(BaselineConfiguration baseline) =>
        JsonSerializer.Serialize(new ConsumerConfiguration(1, Baseline: baseline), ConsumerConfigurationJsonContext.Default.ConsumerConfiguration)
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
}
