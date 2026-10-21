using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

internal static class SecurityCommand
{
    private const string Usage = "Usage: sharedkernel-repo security <baseline|scan> [--root <path>] [--output <directory>]";

    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error, string workingDirectory, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is not ("baseline" or "scan"))
        {
            await error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 2;
        }

        var root = Path.GetFullPath(workingDirectory);
        string? destination = null;
        try
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            for (var index = 1; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || !seen.Add(args[index]))
                {
                    throw new ArgumentException(Usage);
                }

                switch (args[index])
                {
                    case "--root": root = Path.GetFullPath(args[index + 1], workingDirectory); break;
                    case "--output" when args[0] == "scan": destination = args[index + 1]; break;
                    default: throw new ArgumentException(Usage);
                }
            }

            if (args[0] == "scan" && destination is null)
            {
                throw new ArgumentException(Usage);
            }

            var issues = VerifyBaseline(root, cancellationToken);
            foreach (var issue in issues)
            {
                await error.WriteLineAsync($"security: {RepoConfigToolApplication.EscapeControlCharacters(issue.Path)}: {issue.Message}").ConfigureAwait(false);
            }

            if (issues.Count == 0)
            {
                await output.WriteLineAsync("Security baseline is current.").ConfigureAwait(false);
            }

            if (destination is null)
            {
                return issues.Count == 0 ? 0 : 1;
            }

            var scanResult = await SecurityScanner.Run(root, Path.GetFullPath(destination, root), output, error, cancellationToken).ConfigureAwait(false);
            return scanResult == 0 && issues.Count > 0 ? 1 : scanResult;
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            await error.WriteLineAsync("security: Invalid or unreadable baseline configuration, paths, or arguments.").ConfigureAwait(false);
            return 2;
        }
    }

    private static List<RepoConfigIssue> VerifyBaseline(string root, CancellationToken cancellationToken)
    {
        var policyPath = ResolvePath(root, ".config/security-baseline.json");
        using var policy = JsonDocument.Parse(ReadBounded(policyPath, 1_048_576));
        var configuration = policy.RootElement;
        if (!configuration.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1
            || !configuration.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Array || documents.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Invalid baseline policy.");
        }

        List<RepoConfigIssue> issues = [];
        foreach (var document in documents.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!document.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String
                || !document.TryGetProperty("requiredText", out var required) || required.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Invalid document policy.");
            }

            var relativePath = pathElement.GetString();
            if (relativePath is null || !relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only Markdown document contracts are supported.");
            }

            var path = ResolvePath(root, relativePath);
            var markers = required.EnumerateArray().Select(element => element.ValueKind == JsonValueKind.String ? element.GetString() : null).ToArray();
            if (markers.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("Invalid required text.");
            }

            try
            {
                var content = ReadBounded(path, 4_194_304);
                if (markers.Any(marker => !content.Contains(marker!, StringComparison.Ordinal)))
                {
                    issues.Add(new RepoConfigIssue(relativePath, "Missing required text."));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(new RepoConfigIssue(relativePath, "Missing or unreadable document."));
            }
        }

        return issues;
    }

    internal static string ResolvePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains('\\', StringComparison.Ordinal) || relativePath.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected a repository-relative path.");
        }

        var current = root;
        foreach (var segment in relativePath.Split('/'))
        {
            if (segment is "" or "." or "..")
            {
                throw new InvalidOperationException("Invalid repository-relative path.");
            }

            current = Path.Combine(current, segment);
            if (new FileInfo(current).LinkTarget is not null || new DirectoryInfo(current).LinkTarget is not null)
            {
                throw new InvalidOperationException("Symbolic links are not supported in baseline paths.");
            }
        }

        return current;
    }

    private static string ReadBounded(string path, int maximumCharacters)
    {
        using var reader = File.OpenText(path);
        var buffer = new char[maximumCharacters + 1];
        var count = reader.ReadBlock(buffer, 0, buffer.Length);
        if (count > maximumCharacters)
        {
            throw new IOException("Document exceeds the size limit.");
        }

        return new string(buffer, 0, count);
    }
}
