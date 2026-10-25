using System.Globalization;
using System.IO.Enumeration;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Checks the working-tree content of the tracked Gherkin feature files against the configured rules.
/// </summary>
internal static class GherkinVerifier
{
    /// <summary>
    /// The largest feature file, in bytes, that the tool reads.
    /// </summary>
    public const int MaxFileBytes = 64 * 1024 * 1024;

    /// <summary>
    /// Reports each rule violation in the selected feature files, in path order. Without <paramref name="paths"/>, the
    /// tracked <c>*.feature</c> files that an include pattern matches are selected; otherwise those that a path names
    /// or holds. Excluded files are never selected. A Feature name is reported as a duplicate in each selected file
    /// after the first that has it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A path is not repository-relative or names no tracked feature file, or Git cannot list the index.
    /// </exception>
    /// <exception cref="TimeoutException">Git does not list the index in time.</exception>
    public static async Task<IReadOnlyList<RepoConfigIssue>> Verify(
        string rootPath,
        GherkinConfiguration configuration,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootPath);
        var tracked = await GitTrackedFiles.Read(root, ["*.feature"], cancellationToken).ConfigureAwait(false);
        var allowedTags = configuration.AllowedTagPatterns.Select(GherkinConfiguration.CompileTagPattern).ToArray();
        Dictionary<string, int> featureNames = new(StringComparer.Ordinal);
        List<RepoConfigIssue> issues = [];
        foreach (var path in Select(tracked, configuration, paths))
        {
            var (text, failure) = await Read(root, path, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                issues.Add(new RepoConfigIssue(path, failure));
                continue;
            }

            foreach (var (line, message) in Check(text!, configuration, allowedTags, featureNames))
            {
                issues.Add(new RepoConfigIssue(path, string.Create(CultureInfo.InvariantCulture, $"Line {line}: {message}")));
            }
        }

        return issues;
    }

    private static string[] Select(IReadOnlyList<string> tracked, GherkinConfiguration configuration, IReadOnlyList<string> paths)
    {
        var names = paths.Select(static path => path.TrimEnd('/')).ToArray();
        for (var index = 0; index < names.Length; index++)
        {
            if (!RepositoryPath.IsRelative(names[index]))
            {
                throw new InvalidOperationException($"{paths[index]} must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.");
            }

            if (!tracked.Any(file => Names(names[index], file)))
            {
                throw new InvalidOperationException($"{paths[index]} names no tracked feature file.");
            }
        }

        return tracked
            .Where(file => names.Length == 0 ? Matches(configuration.Include, file) : names.Any(name => Names(name, file)))
            .Where(file => !Matches(configuration.Exclude, file))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool Names(string path, string file) =>
        file == path || (file.StartsWith(path, StringComparison.Ordinal) && file[path.Length] == '/');

    private static bool Matches(string[] patterns, string file) =>
        patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, file, ignoreCase: false));

    private static async Task<(string? Text, string? Failure)> Read(string root, string path, CancellationToken cancellationToken)
    {
        byte[]? content;
        try
        {
            content = await RepositoryFile.Read(Path.Combine(root, path), MaxFileBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return (null, "Is missing from the working tree.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, "Cannot be read.");
        }

        if (content is null)
        {
            return (null, "Exceeds the 64 MiB feature file size limit.");
        }

        // A byte order mark stays part of the first line, so that line is neither a keyword, a tag, nor a comment.
        return Utf8.IsValid(content) ? (Encoding.UTF8.GetString(content), null) : (null, JsonVerifier.DescribeInvalidUtf8(content));
    }

    private static List<(int Line, string Message)> Check(
        string text,
        GherkinConfiguration configuration,
        Regex[] allowedTags,
        Dictionary<string, int> featureNames)
    {
        List<(int Line, string Message)> issues = [];

        // ponytail: only CRLF, CR, and LF end lines; also split on VT, FF, NEL, U+2028, and U+2029 if feature files hold them.
        var content = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (content.Length == 0)
        {
            issues.Add((1, "The file is empty."));
            return issues;
        }

        var lines = content.Split('\n');
        var lineCount = content.EndsWith('\n') ? lines.Length - 1 : lines.Length;
        if (!content.EndsWith('\n'))
        {
            issues.Add((lineCount, "The file does not end with a newline."));
        }

        string? featureName = null;
        var featureLine = 0;
        Dictionary<string, int> scenarioNames = new(StringComparer.Ordinal);
        var hasScenario = false;
        var insideOutline = false;
        var outlineHasExamples = false;
        for (var index = 0; index < lineCount; index++)
        {
            var number = index + 1;
            var raw = lines[index];
            if (raw.Length > 0 && char.IsWhiteSpace(raw[^1]))
            {
                issues.Add((number, "Trailing whitespace is not allowed."));
            }

            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            // Text from the first '#' on is a comment, also inside a name.
            var comment = trimmed.IndexOf('#', StringComparison.Ordinal);
            var line = comment < 0 ? trimmed : trimmed[..comment].TrimEnd();
            if (raw.Contains('\t', StringComparison.Ordinal))
            {
                issues.Add((number, "Tabs are not allowed."));
            }

            if (line[0] == '@')
            {
                foreach (var tag in line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Where(static token => token[0] == '@'))
                {
                    if (!allowedTags.Any(pattern => pattern.IsMatch(tag)))
                    {
                        issues.Add((number, $"Tag '{tag}' is not allowed."));
                    }

                    if (configuration.ForbiddenTags.Contains(tag, StringComparer.Ordinal))
                    {
                        issues.Add((number, $"Tag '{tag}' is forbidden."));
                    }
                }
            }
            else if (IsKeyword(line, "Feature"))
            {
                featureName = CheckName(line, number, "Feature", configuration.FeatureNameMaxLength, issues);
                if (featureName is not null)
                {
                    featureNames[featureName] = featureNames.GetValueOrDefault(featureName) + 1;
                    featureLine = number;
                }
            }
            else if (IsKeyword(line, "Scenario Outline") || IsKeyword(line, "Scenario"))
            {
                if (insideOutline && !outlineHasExamples)
                {
                    issues.Add((number, "A Scenario Outline ends here without an Examples section."));
                }

                if (CheckName(line, number, "Scenario", configuration.ScenarioNameMaxLength, issues) is { } name)
                {
                    scenarioNames[name] = scenarioNames.GetValueOrDefault(name) + 1;
                    if (scenarioNames[name] > 1)
                    {
                        issues.Add((number, $"Duplicate Scenario name '{name}'."));
                    }
                }

                insideOutline = IsKeyword(line, "Scenario Outline");
                outlineHasExamples = false;
                hasScenario = true;
            }
            else if (IsKeyword(line, "Examples"))
            {
                outlineHasExamples |= insideOutline;
            }
        }

        if (insideOutline && !outlineHasExamples)
        {
            issues.Add((lineCount, "A Scenario Outline ends here without an Examples section."));
        }

        if (featureName is null)
        {
            issues.Add((1, "A Feature declaration is required."));
        }
        else if (featureNames[featureName] > 1)
        {
            issues.Add((featureLine, $"Duplicate Feature name '{featureName}'."));
        }

        if (!hasScenario)
        {
            issues.Add((1, "The file has no Scenario."));
        }

        return issues;
    }

    // A keyword stands alone or is followed directly by a colon; "Example:" and "Scenario Template:" are not keywords.
    private static bool IsKeyword(string line, string keyword) =>
        line == keyword || (line.StartsWith(keyword, StringComparison.Ordinal) && line.Length > keyword.Length && line[keyword.Length] == ':');

    // Returns the name after the first colon, or null when it is empty. Length counts Unicode code points.
    private static string? CheckName(string line, int number, string label, int maxLength, List<(int Line, string Message)> issues)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        var name = colon < 0 ? string.Empty : line[(colon + 1)..].Trim();
        if (name.Length == 0)
        {
            issues.Add((number, $"The {label} name is required."));
            return null;
        }

        if (name.EnumerateRunes().Count() > maxLength)
        {
            issues.Add((number, string.Create(CultureInfo.InvariantCulture, $"The {label} name exceeds {maxLength} characters.")));
        }

        return name;
    }
}
