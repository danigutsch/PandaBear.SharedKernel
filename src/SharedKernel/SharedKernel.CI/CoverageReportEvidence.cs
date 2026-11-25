using System.IO.Enumeration;
using System.Text.Json;
using SharedKernel.IO;
using SharedKernel.Versioning;

namespace SharedKernel.CI;

/// <summary>Binds fresh local coverage reports to the Git index tree built and tested by the slice runner.</summary>
internal static class CoverageReportEvidence
{
    private const string FileName = "coverage-evidence.json";
    private const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>Requires tracked worktree files to match the index.</summary>
    /// <exception cref="InvalidOperationException">Tracked worktree files differ from the index.</exception>
    public static async Task RequireTrackedMatch(string root, Func<string[], string, CancellationToken, Task<string>> runGit, CancellationToken ct)
    {
        var unstaged = await runGit(["diff", "--name-only", "-z", "--no-ext-diff", "--ignore-submodules=none", "--"], root, ct).ConfigureAwait(false);
        if (unstaged.Length != 0)
        {
            throw new InvalidOperationException(
                "Tracked files have unstaged changes. Make the working tree match the index, rebuild, and collect fresh coverage reports.");
        }
    }

    /// <summary>Returns the matching staged tree, including any staged changes.</summary>
    /// <exception cref="InvalidOperationException">Tracked worktree files differ from the index, or Git returns an invalid tree object ID.</exception>
    public static async Task<string> ReadTree(string root, Func<string[], string, CancellationToken, Task<string>> runGit, CancellationToken ct)
    {
        await RequireTrackedMatch(root, runGit, ct).ConfigureAwait(false);
        var tree = (await runGit(["write-tree"], root, ct).ConfigureAwait(false)).Trim();
        return ProjectSelectionPlan.IsObjectId(tree) ? tree : throw new InvalidOperationException("Git did not return a staged tree object ID.");
    }

    /// <summary>Writes evidence beside the fresh reports collected by the slice runner.</summary>
    public static async Task Record(string results, string tree, CancellationToken ct)
    {
        var reports = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var report in CoberturaReport.Find(results, [results]))
        {
            var path = Path.GetRelativePath(results, report).Replace('\\', '/');
            var file = RepositoryPath.Parse(path).ResolveUnder(results);
            reports.Add(path, await CandidateManifest.HashPackage(file, ct).ConfigureAwait(false));
        }

        var destination = Path.Combine(results, FileName);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(
                new CoverageReportEvidenceFile(tree, reports), CoverageReportEvidenceJsonContext.Default.CoverageReportEvidenceFile), ct)
                .ConfigureAwait(false);
            File.Move(temporary, destination);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>Reads paths and expected report hashes only after every evidence file matches the requested staged tree.</summary>
    /// <exception cref="InvalidDataException">The evidence contains invalid or conflicting reports.</exception>
    /// <exception cref="InvalidOperationException">The evidence records a different staged tree.</exception>
    public static async Task<Dictionary<string, string>> Read(string root, IReadOnlyList<string> evidence, string tree, CancellationToken ct)
    {
        var reports = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in evidence)
        {
            var file = Path.GetFullPath(input, root);
            var description = "The coverage evidence " + input;
            var record = JsonSerializer.Deserialize(await BoundedFile.Read(file, MaxBytes, description, ct).ConfigureAwait(false),
                CoverageReportEvidenceJsonContext.Default.CoverageReportEvidenceFile)
                ?? throw new InvalidDataException(description + " must contain a JSON object.");
            if (!string.Equals(record.Tree, tree, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(description + " does not match the staged tree. Rebuild and collect fresh coverage reports for this tree.");
            }

            if (record.Reports.Count == 0)
            {
                throw new InvalidDataException(description + " names no coverage reports.");
            }

            foreach (var (path, hash) in record.Reports)
            {
                if (!RepositoryPath.IsValid(path)
                    || !FileSystemName.MatchesSimpleExpression(CoberturaReport.FilePattern, Path.GetFileName(path), ignoreCase: true)
                    || hash is null || hash.Length != 64 || !hash.All(char.IsAsciiHexDigitLower))
                {
                    throw new InvalidDataException(description + " contains an invalid coverage report path or SHA-256 hash.");
                }

                var report = RepositoryPath.Parse(path).ResolveUnder(Path.GetDirectoryName(file)!);
                if (reports.TryGetValue(report, out var previous) && previous != hash)
                {
                    throw new InvalidDataException(description + " conflicts with other evidence for coverage report " + report + ".");
                }

                reports[report] = hash;
            }
        }

        return reports;
    }
}
