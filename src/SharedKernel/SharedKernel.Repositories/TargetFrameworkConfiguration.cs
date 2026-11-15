using System.Globalization;
using System.Text.RegularExpressions;
using PandaBear.IO;

namespace PandaBear.Repositories;

/// <summary>
/// The target framework policy that <c>verify-target-frameworks</c> checks the projects against.
/// </summary>
/// <param name="Projects">The rules in priority order; the first rule whose pattern matches a project applies to it.</param>
/// <param name="CommandFiles">Path patterns of the tracked files whose <c>dotnet run --project</c> lines are checked, if any.</param>
/// <param name="FrameworkProperties">
/// The repository-relative MSBuild file that defines the framework properties that rules reference as <c>$(Name)</c>, or
/// <see langword="null"/> when rules name frameworks literally. When set, other files must not name a .NET framework.
/// </param>
public sealed partial record TargetFrameworkConfiguration(
    IReadOnlyList<TargetFrameworkRule> Projects,
    IReadOnlyList<string>? CommandFiles = null,
    string? FrameworkProperties = null)
{
    private const string PatternHint = "must be a repository-relative path pattern with '/' separators and without empty, '.', or '..' segments.";
    private static readonly string[] RollForwardValues = ["Minor", "Major", "LatestPatch", "LatestMinor", "LatestMajor", "Disable"];

    /// <summary>
    /// Describes the first invalid value with its JSON path, or returns <see langword="null"/> when the section is valid.
    /// </summary>
    public string? FindError()
    {
        if (Projects.Count == 0)
        {
            return "$.targetFrameworks.projects must name at least one rule.";
        }

        if (FrameworkProperties is not null && !RepositoryPath.IsValid(FrameworkProperties))
        {
            return "$.targetFrameworks.frameworkProperties must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.";
        }

        for (var index = 0; index < Projects.Count; index++)
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"$.targetFrameworks.projects[{index}]");
            if (FindError(Projects[index], path) is { } error)
            {
                return error;
            }
        }

        for (var index = 0; index < (CommandFiles?.Count ?? 0); index++)
        {
            if (!RepositoryPath.IsValid(CommandFiles![index]))
            {
                return string.Create(CultureInfo.InvariantCulture, $"$.targetFrameworks.commandFiles[{index}] {PatternHint}");
            }
        }

        return null;
    }

    private string? FindError(TargetFrameworkRule rule, string path)
    {
        if (!RepositoryPath.IsValid(rule.Include))
        {
            return $"{path}.include {PatternHint}";
        }

        if (rule.Frameworks.Count == 0)
        {
            return $"{path}.frameworks must name at least one target framework.";
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rule.Frameworks.Count; index++)
        {
            var element = string.Create(CultureInfo.InvariantCulture, $"{path}.frameworks[{index}]");
            if (PropertyReference().IsMatch(rule.Frameworks[index]))
            {
                if (FrameworkProperties is null)
                {
                    return element + " names a property but $.targetFrameworks.frameworkProperties is not set.";
                }
            }
            else if (!Moniker().IsMatch(rule.Frameworks[index]))
            {
                return element + " must be a target framework moniker such as net10.0 or netstandard2.0.";
            }

            if (!seen.Add(rule.Frameworks[index]))
            {
                return element + " repeats a target framework.";
            }
        }

        return rule.RollForward is null || RollForwardValues.Contains(rule.RollForward, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"{path}.rollForward must be one of {string.Join(", ", RollForwardValues)}.";
    }

    /// <summary>
    /// Returns the property name when <paramref name="framework"/> is a <c>$(Name)</c> reference, otherwise <see langword="null"/>.
    /// </summary>
    internal static string? PropertyName(string framework) =>
        PropertyReference().Match(framework) is { Success: true } match ? match.Groups[1].Value : null;

    [GeneratedRegex(@"^\$\(([A-Za-z_][A-Za-z0-9_.\-]*)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex PropertyReference();

    [GeneratedRegex(@"^[a-z][a-z0-9.\-]*[0-9a-z]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Moniker();
}
