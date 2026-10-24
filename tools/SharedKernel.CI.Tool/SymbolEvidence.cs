using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// Records the revision and the SHA-256 hash of each Release portable PDB of a build, and verifies a checkout against
/// that record before <c>coverage verify</c> trusts the symbols.
/// </summary>
internal static class SymbolEvidence
{
    /// <summary>The default evidence path, relative to the root.</summary>
    public const string DefaultPath = "TestResults/symbol-evidence.json";

    private const string ReleaseSegment = "/bin/Release/";

    private const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>Writes the evidence of the <c>*.pdb</c> files in a <c>bin/Release</c> directory under the symbol directories.</summary>
    /// <exception cref="InvalidOperationException">No such file exists, one is reached through a symbolic link, or Git failed.</exception>
    public static async Task Record(
        string root, CoverageConfiguration coverage, string output, Func<string, string[], string, CancellationToken, Task<string>> run, TextWriter standardOutput, CancellationToken ct)
    {
        var revision = (await run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
        var paths = coverage.SymbolDirectories
            .Select(directory => RepositoryPath.Resolve(root, directory))
            .Where(Directory.Exists)
            .SelectMany(static directory => Directory.EnumerateFiles(directory, "*.pdb", SearchOption.AllDirectories))
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(static path => path.Contains(ReleaseSegment, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (paths.Length == 0)
        {
            throw new InvalidOperationException("No Release portable PDB exists under " + string.Join(", ", coverage.SymbolDirectories) + "; build with -c Release first.");
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            files.Add(path, await Hash(root, path, ct).ConfigureAwait(false));
        }

        var destination = Path.GetFullPath(output, root);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllBytesAsync(
            destination, JsonSerializer.SerializeToUtf8Bytes(new SymbolEvidenceFile(revision, files), SymbolEvidenceJsonContext.Default.SymbolEvidenceFile), ct)
            .ConfigureAwait(false);
        await standardOutput.WriteLineAsync("Recorded " + paths.Length + " Release symbol files of " + revision + " in " + output + ".").ConfigureAwait(false);
    }

    /// <summary>
    /// Requires the evidence to record <paramref name="revision"/>, and each file it names to be a Release portable PDB
    /// under a symbol directory whose content has the recorded hash. Returns the files.
    /// </summary>
    /// <exception cref="InvalidOperationException">The evidence records another revision, or a file differs from its hash.</exception>
    /// <exception cref="InvalidDataException">The evidence names no file, or a file outside the symbol directories.</exception>
    public static async Task<string[]> Verify(string root, CoverageConfiguration coverage, string evidence, string revision, CancellationToken ct)
    {
        var record = JsonSerializer.Deserialize(
            await BoundedFile.Read(evidence, MaxBytes, "The symbol evidence " + evidence, ct).ConfigureAwait(false),
            SymbolEvidenceJsonContext.Default.SymbolEvidenceFile) ?? throw new InvalidDataException("The symbol evidence " + evidence + " must contain a JSON object.");
        if (!string.Equals(record.Revision, revision, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The symbol evidence records revision " + ControlCharacters.Escape(record.Revision) + ", but HEAD is " + revision + "; record the symbols of this revision.");
        }

        if (record.Files.Count == 0)
        {
            throw new InvalidDataException("The symbol evidence " + evidence + " names no symbol file.");
        }

        foreach (var (path, hash) in record.Files)
        {
            if (!RepositoryPath.IsRelative(path)
                || !path.EndsWith(".pdb", StringComparison.Ordinal)
                || !path.Contains(ReleaseSegment, StringComparison.Ordinal)
                || !coverage.SymbolDirectories.Any(directory => path.StartsWith(directory + "/", StringComparison.Ordinal)))
            {
                throw new InvalidDataException("The symbol evidence names " + path + ", which is not a Release .pdb under a symbol directory of the checkout.");
            }

            if (!string.Equals(await Hash(root, path, ct).ConfigureAwait(false), hash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The symbol file " + path + " does not match its evidence.");
            }
        }

        return [.. record.Files.Keys.Order(StringComparer.Ordinal)];
    }

    private static Task<string> Hash(string root, string path, CancellationToken ct) => CandidateManifest.HashPackage(RepositoryPath.Resolve(root, path), ct);
}
