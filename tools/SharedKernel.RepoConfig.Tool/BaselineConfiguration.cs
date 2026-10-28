using System.Globalization;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The files, Git settings, hooks, and variables of <c>baseline verify</c> and <c>baseline apply</c>.
/// </summary>
/// <param name="RepositoryName">Value of the <c>@@repositoryName@@</c> variable.</param>
/// <param name="Files">The managed files, in the order that commands report them.</param>
/// <param name="DefaultBranch">Value of the <c>@@defaultBranch@@</c> variable.</param>
/// <param name="GitConfig">The repository-local Git settings, if any.</param>
/// <param name="Hooks">The tracked hooks directory and its hooks, if any.</param>
internal sealed record BaselineConfiguration(
    string RepositoryName,
    IReadOnlyList<BaselineFileConfiguration>? Files = null,
    string DefaultBranch = "main",
    IReadOnlyList<BaselineGitConfigurationEntry>? GitConfig = null,
    BaselineHooksConfiguration? Hooks = null)
{
    private const int MaxNameLength = 100;
    private const int MaxValueLength = 200;

    /// <summary>Gets the managed files followed by the hook files, each with whether it must be executable.</summary>
    public IReadOnlyList<(BaselineFileConfiguration File, bool Executable)> ManagedFiles =>
    [
        .. (Files ?? []).Select(static file => (file, false)),
        .. (Hooks?.Hooks ?? []).Select(hook => (new BaselineFileConfiguration(Hooks!.Directory + "/" + hook.Name, hook.Profile, hook.Source, hook.OverwriteExisting), true)),
    ];

    /// <summary>Gets the Git settings, followed by <c>core.hooksPath</c> when hooks are configured.</summary>
    public IReadOnlyList<BaselineGitConfigurationEntry> GitSettings =>
    [
        .. GitConfig ?? [],
        .. Hooks is null ? [] : (BaselineGitConfigurationEntry[])[new(BaselineGitSettings.HooksPathKey, Hooks.Directory, Hooks.OverwriteHooksPath)],
    ];

    /// <summary>
    /// Describes the first invalid value with its JSON path, or returns <see langword="null"/> when the section is valid.
    /// </summary>
    public string? FindError()
    {
        if (RepositoryName.Length is 0 or > MaxNameLength || !RepositoryName.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
        {
            return "$.baseline.repositoryName must be 1 to 100 letters, digits, '.', '-', or '_'.";
        }

        if (DefaultBranch.Length is 0 or > MaxNameLength
            || DefaultBranch[0] is '-' or '/'
            || !DefaultBranch.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '/'))
        {
            return "$.baseline.defaultBranch must be 1 to 100 letters, digits, '.', '-', '_', or '/' and must not start with '-' or '/'.";
        }

        if ((Files ?? []).Count == 0 && (GitConfig ?? []).Count == 0 && Hooks is null)
        {
            return "$.baseline must name at least one of files, gitConfig, or hooks.";
        }

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        var files = Files ?? [];
        for (var index = 0; index < files.Count; index++)
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"$.baseline.files[{index}]");
            if (FindError(files[index], path) is { } error)
            {
                return error;
            }

            if (!paths.Add(files[index].Path))
            {
                return path + ".path repeats an earlier path, ignoring case.";
            }
        }

        return FindGitConfigError() ?? FindHooksError(paths);
    }

    private string? FindGitConfigError()
    {
        var settings = GitConfig ?? [];
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < settings.Count; index++)
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"$.baseline.gitConfig[{index}]");
            var (key, value, _) = settings[index];
            if (!BaselineGitSettings.AllowedKeys.Contains(key))
            {
                return path + ".key must be one of: " + string.Join(", ", BaselineGitSettings.AllowedKeys.Order(StringComparer.OrdinalIgnoreCase))
                    + ". Other keys can run code or redirect authentication; core.hooksPath is set through hooks.";
            }

            if (value.Length is 0 or > MaxValueLength || value.Any(char.IsControl) || value[0] == '-')
            {
                return path + ".value must be 1 to 200 characters without control characters and must not start with '-'.";
            }

            if (!keys.Add(key))
            {
                return path + ".key repeats an earlier key, ignoring case.";
            }
        }

        return null;
    }

    private string? FindHooksError(HashSet<string> paths)
    {
        if (Hooks is not { } hooks)
        {
            return null;
        }

        if (!IsSafePath(hooks.Directory))
        {
            return "$.baseline.hooks.directory " + PathRule;
        }

        if (hooks.Hooks.Count == 0)
        {
            return "$.baseline.hooks.hooks must name at least one hook.";
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        for (var index = 0; index < hooks.Hooks.Count; index++)
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"$.baseline.hooks.hooks[{index}]");
            var hook = hooks.Hooks[index];
            if (!BaselineGitSettings.HookNames.Contains(hook.Name))
            {
                return path + ".name must be one of: " + string.Join(", ", BaselineGitSettings.HookNames.Order(StringComparer.Ordinal)) + ".";
            }

            if (FindSourceError(hook.Profile, hook.Source, hooks.Directory + "/" + hook.Name, path) is { } error)
            {
                return error;
            }

            if (!names.Add(hook.Name) || !paths.Add(hooks.Directory + "/" + hook.Name))
            {
                return path + ".name repeats an earlier path, ignoring case.";
            }
        }

        return null;
    }

    private static string? FindError(BaselineFileConfiguration file, string path)
    {
        if (!IsSafePath(file.Path)
            || file.Path.Equals(ConsumerConfigurationFile.RelativePath, StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase))
        {
            return path + ".path " + PathRule + " It is not the configuration file and not a JSON file, which has no comment for the managed-file marker.";
        }

        return FindSourceError(file.Profile, file.Source, file.Path, path);
    }

    private static string? FindSourceError(string? profile, string? source, string target, string path)
    {
        if ((profile is null) == (source is null))
        {
            return path + " must name exactly one of profile and source.";
        }

        if (profile is not null && !BaselineProfiles.TryGet(profile, out _))
        {
            return path + ".profile must be one of: " + string.Join(", ", BaselineProfiles.Ids) + ".";
        }

        if (source is not null
            && (!RepositoryPath.IsRelative(source) || source.Any(char.IsControl) || source.Equals(target, StringComparison.OrdinalIgnoreCase)))
        {
            return path + ".source must be a repository-relative path with '/' separators and without empty, '.', or '..' segments, and differ from the target path.";
        }

        return null;
    }

    private const string PathRule =
        "must be a repository-relative path with '/' separators and without empty, '.', or '..' segments, without a .git directory at any depth, and without a segment that contains '~' or ends in '.' or a space (Windows aliases of .git and of JSON suffixes).";

    private static bool IsSafePath(string path) =>
        RepositoryPath.IsRelative(path) && !path.Any(char.IsControl) && !path.Split('/').Any(IsUnsafeSegment);

    // Windows resolves "x.json." and ".git " to "x.json" and ".git", and "GIT~1" to ".git"; the rule applies on every
    // platform so that one configuration behaves the same everywhere.
    private static bool IsUnsafeSegment(string segment) =>
        segment.Contains('~', StringComparison.Ordinal)
        || segment[^1] is '.' or ' '
        || segment.TrimEnd('.', ' ').Equals(".git", StringComparison.OrdinalIgnoreCase);
}
