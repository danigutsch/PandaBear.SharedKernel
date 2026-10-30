using System.Text.Json;
using System.Text.RegularExpressions;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Verifies that every external <c>uses:</c> reference in workflows and composite actions pins the full commit SHA
/// that the action pin manifest approves and repeats the approved version as a trailing comment.
/// </summary>
internal static partial class ActionPinVerifier
{
    /// <summary>
    /// The manifest path relative to the repository root.
    /// </summary>
    public const string DefaultManifestPath = ".github/action-pins.json";

    private const int MaxManifestBytes = 1024 * 1024;

    /// <summary>
    /// Reports each external reference in <c>.github/workflows/*.yml</c>, <c>.github/workflows/*.yaml</c>, and every
    /// <c>action.yml</c> or <c>action.yaml</c> under <c>.github/actions</c> that is not pinned to a full commit SHA,
    /// is absent from the manifest, pins another SHA than the manifest, or lacks the manifest version as its comment.
    /// Local <c>./</c> references are not checked; <c>docker://</c> references are never approved.
    /// </summary>
    /// <exception cref="InvalidOperationException">The manifest is missing, unreadable, too large, or invalid.</exception>
    public static async Task<IReadOnlyList<RepoConfigIssue>> Verify(string rootPath, string? manifestPath, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootPath);
        var manifest = await ReadManifest(manifestPath is null ? Path.Combine(root, DefaultManifestPath) : Path.GetFullPath(manifestPath, root), cancellationToken).ConfigureAwait(false);
        var approved = manifest.Actions.ToDictionary(static pin => pin.Repository, StringComparer.OrdinalIgnoreCase);
        var firstParty = new HashSet<string>(manifest.FirstParty, StringComparer.OrdinalIgnoreCase);
        List<RepoConfigIssue> issues = [];
        foreach (var file in FindFiles(root))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < lines.Length; index++)
            {
                if (UsesLine().Match(lines[index]) is { Success: true } match
                    && Check(match.Groups["ref"].Value.Trim('\'', '"'), match.Groups["comment"].Value, approved, firstParty) is { } message)
                {
                    issues.Add(new RepoConfigIssue(relative, $"Line {index + 1}: {message}"));
                }
            }
        }

        return issues;
    }

    private static string? Check(string reference, string comment, Dictionary<string, ActionPin> approved, HashSet<string> firstParty)
    {
        if (reference.StartsWith("./", StringComparison.Ordinal))
        {
            return null;
        }

        if (reference.StartsWith("docker://", StringComparison.OrdinalIgnoreCase))
        {
            return $"'{reference}' is a container reference, which the manifest cannot approve.";
        }

        var at = reference.LastIndexOf('@');
        var name = at < 0 ? reference : reference[..at];
        var pinned = at < 0 ? string.Empty : reference[(at + 1)..];
        if (!FullSha().IsMatch(pinned))
        {
            return $"'{name}' must pin a full 40-character lowercase commit SHA, not '{pinned}'.";
        }

        var segments = name.Split('/');
        var repository = segments.Length >= 2 ? segments[0] + "/" + segments[1] : name;
        if (firstParty.Contains(repository))
        {
            return null;
        }

        if (!approved.TryGetValue(repository, out var pin))
        {
            return $"'{repository}' is not in the action pin manifest.";
        }

        if (!string.Equals(pinned, pin.Sha, StringComparison.Ordinal))
        {
            return $"'{repository}' pins {pinned} but the manifest approves {pin.Sha} ({pin.Version}).";
        }

        return string.Equals(comment.Trim(), pin.Version, StringComparison.Ordinal)
            ? null
            : $"'{repository}' needs the trailing comment '# {pin.Version}'.";
    }

    private static IEnumerable<string> FindFiles(string root)
    {
        var workflows = Path.Combine(root, ".github", "workflows");
        var actions = Path.Combine(root, ".github", "actions");
        var files = Directory.Exists(workflows)
            ? Directory.EnumerateFiles(workflows).Where(static file => file.EndsWith(".yml", StringComparison.Ordinal) || file.EndsWith(".yaml", StringComparison.Ordinal))
            : [];
        var composites = Directory.Exists(actions)
            ? Directory.EnumerateFiles(actions, "*", SearchOption.AllDirectories).Where(static file => Path.GetFileName(file) is "action.yml" or "action.yaml")
            : [];
        return files.Concat(composites).Order(StringComparer.Ordinal);
    }

    private static async Task<ActionPinManifest> ReadManifest(string path, CancellationToken cancellationToken)
    {
        byte[]? content;
        try
        {
            content = await RepositoryFile.Read(path, MaxManifestBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException("The action pin manifest does not exist.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("The action pin manifest cannot be read.", exception);
        }

        if (content is null)
        {
            throw new InvalidOperationException("The action pin manifest exceeds the 1 MiB size limit.");
        }

        ActionPinManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(RepositoryFile.WithoutUtf8ByteOrderMark(content).Span, ActionPinManifestJsonContext.Default.ActionPinManifest);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The action pin manifest is not valid at {exception.Path ?? "$"}.", exception);
        }

        if (manifest is not { Version: 1 })
        {
            throw new InvalidOperationException("The action pin manifest must be a JSON object with version 1.");
        }

        HashSet<string> repositories = new(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in manifest.Actions)
        {
            if (!RepositoryName().IsMatch(pin.Repository) || !FullSha().IsMatch(pin.Sha) || string.IsNullOrWhiteSpace(pin.Version) || pin.Version.Trim() != pin.Version)
            {
                throw new InvalidOperationException($"The action pin manifest entry '{pin.Repository}' needs an owner/repository name, a full lowercase commit SHA, and a version.");
            }

            if (!repositories.Add(pin.Repository))
            {
                throw new InvalidOperationException($"The action pin manifest repeats '{pin.Repository}'.");
            }
        }

        return manifest.FirstParty.All(static repository => RepositoryName().IsMatch(repository))
            ? manifest
            : throw new InvalidOperationException("Every action pin manifest firstParty entry must be an owner/repository name.");
    }

    [GeneratedRegex(@"^\s*(?:-\s+)?uses:\s*(?<ref>\S+)(?:\s+#\s*(?<comment>.*))?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UsesLine();

    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FullSha();

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepositoryName();
}
