using System.Globalization;
using System.IO.Enumeration;
using System.Text;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Verifies the local links, Markdown anchors, and issue references of documentation files in the working tree.
/// </summary>
internal static class LinkVerifier
{
    private const int MaxFileBytes = 64 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly EnumerationOptions DirectoryEntries = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
    };

    /// <summary>
    /// Scans the files that <paramref name="paths"/> name, or the files that match an include pattern when none is
    /// given, and returns how many it scanned with the findings in path order. A path names a Markdown or text file,
    /// or a directory whose Markdown files are scanned recursively; other paths are skipped. Files resolve through
    /// symbolic links, and a resolved file outside the root or matching an exclude pattern is skipped.
    /// </summary>
    public static async Task<(int FileCount, IReadOnlyList<RepoConfigIssue> Issues)> Verify(
        string rootPath,
        LinkConfiguration configuration,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var root = RealPath.Resolve(Path.GetFullPath(rootPath));
        var candidates = paths.Count > 0 ? Requested(root, paths) : Included(root, configuration.Include);
        var files = candidates
            .Select(RealPath.Resolve)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Select(path => (Path: path, Relative: Relative(root, path)))
            .Where(file => file.Relative is not null && !configuration.Exclude.Any(pattern => Matches(pattern, file.Relative)))
            .OrderBy(file => file.Relative, StringComparer.Ordinal)
            .ToArray();
        List<RepoConfigIssue> issues = [];
        foreach (var (path, relative) in files)
        {
            var (text, failure) = await ReadText(path, cancellationToken).ConfigureAwait(false);
            if (text is null)
            {
                issues.Add(new RepoConfigIssue(relative!, failure!));
                continue;
            }

            foreach (var (line, message) in await Validate(root, path, relative!, text, cancellationToken).ConfigureAwait(false))
            {
                issues.Add(new RepoConfigIssue(string.Create(CultureInfo.InvariantCulture, $"{relative}:{line}"), message));
            }
        }

        return (files.Length, issues);
    }

    private static async Task<List<(int Line, string Message)>> Validate(string root, string path, string relative, string text, CancellationToken cancellationToken)
    {
        var lines = MarkdownLinkRules.SplitLines(text);
        var anchors = MarkdownLinkRules.Anchors(text);
        List<(int Line, string Message)> findings = [];
        foreach (var (number, line) in MarkdownLinkRules.NonFencedLines(lines))
        {
            findings.AddRange(MarkdownLinkRules.PolicyFindings(line).Select(message => (number, message)));
        }

        foreach (var (number, line) in MarkdownLinkRules.NonFencedLines(lines))
        {
            foreach (var target in MarkdownLinkRules.LinkTargets(line))
            {
                if (MarkdownLinkRules.IsGitHubIssueOrPullRequestUrl(MarkdownLinkRules.NormalizeTarget(target)))
                {
                    findings.Add((number, MarkdownLinkRules.GitHubIssueOrPullRequestUrl));
                }

                if (await ValidateTarget(root, path, relative, anchors, target, cancellationToken).ConfigureAwait(false) is { } message)
                {
                    findings.Add((number, message));
                }
            }
        }

        return findings;
    }

    private static async Task<string?> ValidateTarget(string root, string path, string relative, HashSet<string> anchors, string target, CancellationToken cancellationToken)
    {
        if (target.Length == 0)
        {
            return "empty link target.";
        }

        var normalized = MarkdownLinkRules.NormalizeTarget(target);
        if (normalized.Length == 0 || MarkdownLinkRules.IsExternalOrSpecial(normalized))
        {
            return null;
        }

        var separator = normalized.IndexOf('#', StringComparison.Ordinal);
        var targetPath = separator < 0 ? normalized : normalized[..separator];
        var fragment = separator < 0 ? string.Empty : normalized[(separator + 1)..];
        var resolved = targetPath.Length == 0
            ? path
            : RealPath.Resolve(Path.Combine(Path.GetDirectoryName(path)!, MarkdownLinkRules.Unquote(targetPath)));
        if (Relative(root, resolved) is null)
        {
            return "link escapes repository: " + target;
        }

        if (!File.Exists(resolved) && !Directory.Exists(resolved))
        {
            return "missing local link target: " + target;
        }

        if (fragment.Length == 0 || MarkdownLinkRules.Lower(MarkdownLinkRules.Suffix(resolved)) != ".md")
        {
            return null;
        }

        // An unreadable target has no anchors, so its fragment is reported as missing.
        var targetAnchors = string.Equals(resolved, path, PathComparison)
            ? anchors
            : MarkdownLinkRules.Anchors((await ReadText(resolved, cancellationToken).ConfigureAwait(false)).Text ?? string.Empty);
        return targetAnchors.Contains(MarkdownLinkRules.Lower(MarkdownLinkRules.Unquote(fragment)))
            ? null
            : $"missing Markdown anchor '{fragment}' in {(targetPath.Length == 0 ? relative : targetPath)}";
    }

    private static List<string> Requested(string root, IReadOnlyList<string> paths)
    {
        List<string> candidates = [];
        foreach (var value in paths)
        {
            // Symbolic links resolve before '..' applies, so the path is not normalized first.
            var path = RealPath.Resolve(Path.Combine(root, value));
            if (Directory.Exists(path))
            {
                candidates.AddRange(Walk(path, static entry => entry.Name.EndsWith(".md", StringComparison.Ordinal)));
            }
            else if (MarkdownLinkRules.Lower(MarkdownLinkRules.Suffix(path)) is ".md" or ".txt")
            {
                candidates.Add(path);
            }
        }

        return candidates;
    }

    private static List<string> Included(string root, string[] include)
    {
        List<string> candidates = [];
        foreach (var pattern in include)
        {
            var wildcard = pattern.IndexOfAny(['*', '?']);
            if (wildcard < 0)
            {
                var path = Path.Combine(root, pattern);
                if (File.Exists(path) || Directory.Exists(path))
                {
                    candidates.Add(path);
                }

                continue;
            }

            // Only the directory before the first wildcard can hold a match.
            var directory = pattern.LastIndexOf('/', wildcard);
            var start = directory < 0 ? root : Path.Combine(root, pattern[..directory]);
            if (Directory.Exists(start))
            {
                candidates.AddRange(Walk(start, entry => Matches(pattern, Path.GetRelativePath(root, entry.FullName).Replace('\\', '/'))));
            }
        }

        return candidates;
    }

    // Like Python's Path.rglob, the walk lists every entry, but it does not descend into symbolic links to directories.
    private static IEnumerable<string> Walk(string directory, Func<FileSystemInfo, bool> matches)
    {
        Stack<DirectoryInfo> pending = new([new DirectoryInfo(directory)]);
        while (pending.TryPop(out var current))
        {
            foreach (var entry in current.EnumerateFileSystemInfos("*", DirectoryEntries))
            {
                if (matches(entry))
                {
                    yield return entry.FullName;
                }

                if (entry is DirectoryInfo subdirectory && entry.LinkTarget is null)
                {
                    pending.Push(subdirectory);
                }
            }
        }
    }

    private static async Task<(string? Text, string? Failure)> ReadText(string path, CancellationToken cancellationToken)
    {
        byte[]? content;
        try
        {
            content = await RepositoryFile.Read(path, MaxFileBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, "cannot be read.");
        }

        if (content is null)
        {
            return (null, "exceeds the 64 MiB size limit.");
        }

        try
        {
            // Like Python's read_text, a byte order mark stays in the text.
            return (StrictUtf8.GetString(content), null);
        }
        catch (DecoderFallbackException)
        {
            return (null, "is not valid UTF-8.");
        }
    }

    private static string? Relative(string root, string path)
    {
        if (string.Equals(path, root, PathComparison))
        {
            return ".";
        }

        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, PathComparison) ? path[prefix.Length..].Replace('\\', '/') : null;
    }

    private static bool Matches(string pattern, string path) =>
        FileSystemName.MatchesSimpleExpression(pattern, path, ignoreCase: false);
}
