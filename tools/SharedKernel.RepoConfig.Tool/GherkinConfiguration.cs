using System.Globalization;
using System.Text.RegularExpressions;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The feature files that <c>verify-gherkin</c> checks and the tag and name rules that they follow.
/// </summary>
/// <param name="Include">Path patterns of the tracked <c>*.feature</c> files to check when no path is given.</param>
/// <param name="Exclude">Path patterns of selected feature files that are not checked, even when a path names them.</param>
/// <param name="AllowedTagPatterns">Regular expressions, each matched against a whole tag, of the tags that are allowed.</param>
/// <param name="ForbiddenTags">Tags that are forbidden even when a pattern allows them.</param>
/// <param name="FeatureNameMaxLength">The most characters that a Feature name may have.</param>
/// <param name="ScenarioNameMaxLength">The most characters that a Scenario or Scenario Outline name may have.</param>
internal sealed record GherkinConfiguration(
    string[] Include,
    string[] Exclude,
    string[] AllowedTagPatterns,
    string[] ForbiddenTags,
    int FeatureNameMaxLength,
    int ScenarioNameMaxLength)
{
    private const RegexOptions Options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Compiles an allowed tag pattern so that it matches only a whole tag.
    /// </summary>
    /// <exception cref="ArgumentException">The pattern is not a regular expression.</exception>
    /// <exception cref="NotSupportedException">The pattern uses a construct that non-backtracking matching lacks.</exception>
    public static Regex CompileTagPattern(string pattern)
    {
        // Parsing the pattern alone first rejects one such as "a)|(b" that would close the anchored group early.
        _ = new Regex(pattern, Options, MatchTimeout);
        return new Regex(@"\A(?:" + pattern + @")\z", Options, MatchTimeout);
    }

    /// <summary>
    /// Describes the first invalid value with its JSON path, or returns <see langword="null"/> when the section is valid.
    /// </summary>
    public string? FindError()
    {
        if (Include.Length == 0)
        {
            return "$.gherkin.include must name at least one pattern.";
        }

        var error = FindPatternError(Include, "$.gherkin.include") ?? FindPatternError(Exclude, "$.gherkin.exclude");
        if (error is not null)
        {
            return error;
        }

        for (var index = 0; index < AllowedTagPatterns.Length; index++)
        {
            // An empty pattern would allow only an empty tag, which a tag line never holds.
            if (string.IsNullOrEmpty(AllowedTagPatterns[index]) || !Compiles(AllowedTagPatterns[index]))
            {
                return Element("$.gherkin.allowedTagPatterns", index) + " must be a regular expression that RegexOptions.NonBacktracking supports.";
            }
        }

        for (var index = 0; index < ForbiddenTags.Length; index++)
        {
            var tag = ForbiddenTags[index];
            if (tag is not ['@', ..] || tag.Any(char.IsWhiteSpace))
            {
                return Element("$.gherkin.forbiddenTags", index) + " must be a tag that starts with '@' and has no whitespace.";
            }
        }

        if (FeatureNameMaxLength < 1)
        {
            return "$.gherkin.featureNameMaxLength must be at least 1.";
        }

        return ScenarioNameMaxLength < 1 ? "$.gherkin.scenarioNameMaxLength must be at least 1." : null;
    }

    private static bool Compiles(string pattern)
    {
        try
        {
            _ = CompileTagPattern(pattern);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string? FindPatternError(string[] patterns, string path)
    {
        for (var index = 0; index < patterns.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(patterns[index]) || patterns[index].Contains('\\', StringComparison.Ordinal))
            {
                return Element(path, index) + " must be a non-empty value with '/' separators.";
            }
        }

        return null;
    }

    private static string Element(string path, int index) => path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
}
