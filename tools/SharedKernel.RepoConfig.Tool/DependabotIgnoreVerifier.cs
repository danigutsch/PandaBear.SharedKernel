using System.Text.RegularExpressions;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Verifies that every <c>ignore</c> entry in the Dependabot configuration names <c>update-types</c>. GitHub applies an
/// entry without them, which expands to every version, to Dependabot security updates as well, so the dependency would
/// never receive a security pull request.
/// </summary>
internal static partial class DependabotIgnoreVerifier
{
    private static readonly string[] RelativePaths = [".github/dependabot.yml", ".github/dependabot.yaml"];

    /// <summary>
    /// Reports each <c>ignore</c> list entry of the Dependabot configuration that has no <c>update-types</c> key. The
    /// check is line based and expects block-style YAML; an inline <c>ignore</c> value is reported because it cannot be checked. A root without the configuration is an error.
    /// </summary>
    /// <exception cref="InvalidOperationException">The root has no Dependabot configuration.</exception>
    public static async Task<IReadOnlyList<RepoConfigIssue>> Verify(string rootPath, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootPath);
        var relative = RelativePaths.FirstOrDefault(path => File.Exists(Path.Combine(root, path)))
            ?? throw new InvalidOperationException("The root has no .github/dependabot.yml or .github/dependabot.yaml file.");
        var lines = await File.ReadAllLinesAsync(Path.Combine(root, relative), cancellationToken).ConfigureAwait(false);
        List<RepoConfigIssue> issues = [];
        var ignoreIndent = -1;
        var entryIndent = -1;
        var entryLine = 0;
        var entryName = string.Empty;
        var entryScoped = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var text = lines[index].TrimStart();
            if (text.Length == 0 || text[0] == '#')
            {
                continue;
            }

            var indent = lines[index].Length - text.Length;
            var isItem = text.StartsWith("- ", StringComparison.Ordinal);
            if (ignoreIndent >= 0 && (indent < ignoreIndent || (indent == ignoreIndent && !isItem)))
            {
                Flush();
                ignoreIndent = -1;
            }

            if (ignoreIndent < 0)
            {
                ignoreIndent = IgnoreKey().IsMatch(text) ? indent : -1;
                if (ignoreIndent < 0 && InlineIgnore().IsMatch(text))
                {
                    issues.Add(new RepoConfigIssue(relative, $"Line {index + 1}: The ignore list must be block-style; this line cannot be checked."));
                }

                continue;
            }

            var body = isItem ? text[2..].TrimStart() : text;
            if (isItem && (entryIndent < 0 || indent <= entryIndent))
            {
                Flush();
                entryIndent = indent;
                entryLine = index + 1;
                entryName = NameKey().Match(body) is { Success: true } name ? Unquote(name.Groups["name"].Value) : string.Empty;
                entryScoped = false;
            }

            entryScoped |= body.StartsWith("update-types:", StringComparison.Ordinal);
            if (entryName.Length == 0 && NameKey().Match(body) is { Success: true } later)
            {
                entryName = Unquote(later.Groups["name"].Value);
            }
        }

        Flush();
        return issues;

        void Flush()
        {
            if (entryIndent >= 0 && !entryScoped)
            {
                issues.Add(new RepoConfigIssue(
                    relative,
                    $"Line {entryLine}: The ignore entry '{entryName}' has no update-types, so it also blocks security updates; add the version-update types to ignore."));
            }

            entryIndent = -1;
        }
    }

    private static string Unquote(string value)
    {
        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        return (comment < 0 ? value : value[..comment]).Trim().Trim('\'', '"');
    }

    [GeneratedRegex(@"^ignore:\s*(?:#.*)?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IgnoreKey();

    [GeneratedRegex(@"^ignore:\s*[^\s#]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineIgnore();

    [GeneratedRegex(@"^dependency-name:\s*(?<name>.+?)\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NameKey();
}
