using System.Text.Json;
using PandaBear.IO;
using PandaBear.InputNormalization;

namespace PandaBear.Repositories;

/// <summary>Implements the security command: verifies the security baseline policy and optionally runs the secret scan.</summary>
public static class RepositorySecurity
{
    /// <summary>Verifies the baseline, then scans for secrets when a report destination is given, and returns the process exit code (0 clean, 1 findings or baseline issues, 2 invalid input, 3 scanner failure).</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> or <paramref name="error"/> is <see langword="null"/>.</exception>
    public static async Task<int> Run(string root, string? destination, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            root = Path.GetFullPath(root);
            var issues = VerifyBaseline(root, cancellationToken);
            foreach (var issue in issues)
            {
                await error.WriteLineAsync($"security: {ControlCharacters.Escape(issue.Path)}: {issue.Message}".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            }

            if (issues.Count == 0)
            {
                await output.WriteLineAsync("Security baseline is current.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
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
            await error.WriteLineAsync("security: Invalid or unreadable baseline configuration, paths, or arguments.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return 2;
        }
    }

    private static List<VerificationIssue> VerifyBaseline(string root, CancellationToken cancellationToken)
    {
        var policyPath = RepositoryPath.Parse(".config/security-baseline.json").ResolveUnder(root);
        using var policy = JsonDocument.Parse(ReadBounded(policyPath, 1_048_576));
        var configuration = policy.RootElement;
        if (!configuration.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1
            || !configuration.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Array || documents.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Invalid baseline policy.");
        }

        List<VerificationIssue> issues = [];
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

            var path = RepositoryPath.Parse(relativePath).ResolveUnder(root);
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
                    issues.Add(new VerificationIssue(relativePath, "Missing required text."));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(new VerificationIssue(relativePath, "Missing or unreadable document."));
            }
        }

        return issues;
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
