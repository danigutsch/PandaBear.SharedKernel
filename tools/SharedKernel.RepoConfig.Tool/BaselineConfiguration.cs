using System.Globalization;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The files and variables of <c>baseline verify</c> and <c>baseline apply</c>.
/// </summary>
/// <param name="RepositoryName">Value of the <c>@@repositoryName@@</c> variable.</param>
/// <param name="Files">The managed files, in the order that commands report them.</param>
/// <param name="DefaultBranch">Value of the <c>@@defaultBranch@@</c> variable.</param>
internal sealed record BaselineConfiguration(
    string RepositoryName,
    IReadOnlyList<BaselineFileConfiguration> Files,
    string DefaultBranch = "main")
{
    private const int MaxNameLength = 100;

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

        if (Files.Count == 0)
        {
            return "$.baseline.files must name at least one file.";
        }

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < Files.Count; index++)
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"$.baseline.files[{index}]");
            if (FindError(Files[index], path) is { } error)
            {
                return error;
            }

            if (!paths.Add(Files[index].Path))
            {
                return path + ".path repeats an earlier path, ignoring case.";
            }
        }

        return null;
    }

    private static string? FindError(BaselineFileConfiguration file, string path)
    {
        if (!RepositoryPath.IsRelative(file.Path)
            || file.Path.Any(char.IsControl)
            || file.Path.Split('/')[0].Equals(".git", StringComparison.OrdinalIgnoreCase)
            || file.Path.Equals(ConsumerConfigurationFile.RelativePath, StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase))
        {
            return path + ".path must be a repository-relative path with '/' separators and without empty, '.', or '..' segments, outside .git, not the configuration file, and not a JSON file, which has no comment for the managed-file marker.";
        }

        if ((file.Profile is null) == (file.Source is null))
        {
            return path + " must name exactly one of profile and source.";
        }

        if (file.Profile is { } profile && !BaselineProfiles.TryGet(profile, out _))
        {
            return path + ".profile must be one of: " + string.Join(", ", BaselineProfiles.Ids) + ".";
        }

        if (file.Source is { } source
            && (!RepositoryPath.IsRelative(source) || source.Any(char.IsControl) || source.Equals(file.Path, StringComparison.OrdinalIgnoreCase)))
        {
            return path + ".source must be a repository-relative path with '/' separators and without empty, '.', or '..' segments, and differ from path.";
        }

        return null;
    }
}
