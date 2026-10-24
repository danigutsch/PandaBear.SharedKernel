using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// Verifies that the tests cover enough of the executable lines and conditions that a change adds, and writes
/// <c>coverage-gate.json</c> whether the check passes, fails, or cannot run.
/// </summary>
internal static class CoverageGate
{
    /// <summary>The default gate evidence path, relative to the root.</summary>
    public const string DefaultOutput = "TestResults/coverage-gate.json";

    private const int MaxConfigurationBytes = 1024 * 1024;

    /// <summary>
    /// Arguments of the comparison, before the revision range. Each option overrides repository configuration that could
    /// hide or move changed lines: external diff drivers, text conversion, binary attributes, colors, path quoting,
    /// prefixes, and context between hunks.
    /// </summary>
    public static readonly string[] DiffArguments =
    [
        "-c", "core.quotePath=false", "diff", "--no-ext-diff", "--no-textconv", "--no-color", "--src-prefix=a/", "--dst-prefix=b/",
        "--unified=0", "--inter-hunk-context=0", "--text", "--find-renames",
    ];

    /// <summary>Checks the symbol evidence, then the coverage of the lines added since <paramref name="baseRevision"/>.</summary>
    /// <exception cref="InvalidOperationException">The coverage is below the threshold, the evidence does not match, or Git failed.</exception>
    /// <exception cref="InvalidDataException">A report, the diff, or the evidence is invalid, or changed code has no coverage.</exception>
    public static async Task Verify(
        string root,
        string[] reports,
        string baseRevision,
        string symbols,
        string output,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter standardOutput,
        CancellationToken ct)
    {
        var threshold = 0;
        long covered = 0;
        long total = 0;
        var passed = false;
        string? configurationHash = null;
        string? revision = null;
        string[] parents = [];
        string[] files = [];
        string? failure = null;
        try
        {
            // The value reaches git diff, so it cannot be an option. It is checked here, not while parsing, so a caller
            // test can parse a command line whose base is a shell variable.
            if (!ProjectSelectionPlan.IsObjectId(baseRevision))
            {
                throw new InvalidOperationException("--base requires a full hexadecimal Git object ID.");
            }

            var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
            var coverage = configuration.Coverage ?? throw CiConfigurationFile.Invalid("$.coverage is required by the coverage commands.");
            threshold = coverage.ThresholdPercent;
            configurationHash = Convert.ToHexStringLower(SHA256.HashData(await BoundedFile.Read(
                RepositoryPath.Resolve(root, CiConfigurationFile.RelativePath), MaxConfigurationBytes, CiConfigurationFile.RelativePath, ct).ConfigureAwait(false)));
            revision = (await run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
            parents = (await run("git", ["rev-parse", "HEAD^@"], root, ct).ConfigureAwait(false)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // The symbols decide which uncovered files count as having no executable code, so they are checked first.
            var symbolFiles = await SymbolEvidence.Verify(root, coverage, Path.GetFullPath(symbols, root), revision, ct).ConfigureAwait(false);
            var patch = await run("git", [.. DiffArguments, baseRevision + "...HEAD", "--"], root, ct).ConfigureAwait(false);
            bool Included(string path) => ProjectSelector.Match(coverage.Include, path) is not null && ProjectSelector.Match(coverage.Exclude, path) is null;
            var changes = ChangedLines.Parse(patch, Included);
            files = CoberturaReport.Find(root, reports);
            var report = CoberturaReport.Read(root, files, Included);
            foreach (var (path, changedLines) in changes.OrderBy(static change => change.Key, StringComparer.Ordinal))
            {
                if (!report.TryGetValue(path, out var lines))
                {
                    if (PortableSymbols.HasOnlyNonExecutableCode(root, path, symbolFiles))
                    {
                        continue;
                    }

                    throw new InvalidDataException("The changed source file " + path + " is in no coverage report, and the symbols do not show it without executable code.");
                }

                foreach (var number in changedLines)
                {
                    if (lines.TryGetValue(number, out var line))
                    {
                        total += 1L + line.Branches;
                        covered += (line.Hit ? 1L : 0L) + line.CoveredBranches;
                    }
                }
            }

            passed = total == 0 || covered * 100 >= total * threshold;
            await standardOutput.WriteLineAsync(
                string.Create(CultureInfo.InvariantCulture, $"Coverage: {covered}/{total} changed executable lines and conditions; required {threshold}%.")).ConfigureAwait(false);
            if (!passed)
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Changed-code coverage {covered}/{total} is below the required {threshold}%."));
            }
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            throw;
        }
        finally
        {
            // The evidence is written whether the check passes, fails, or is canceled, so the job can upload it.
            await Write(Path.GetFullPath(output, root), root, passed, threshold, covered, total, revision, parents, baseRevision, configurationHash, files, failure)
                .ConfigureAwait(false);
        }
    }

    private static async Task Write(
        string destination,
        string root,
        bool passed,
        int threshold,
        long covered,
        long total,
        string? revision,
        string[] parents,
        string baseRevision,
        string? configurationHash,
        string[] files,
        string? failure)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("passed", passed);
            writer.WriteNumber("thresholdPercent", threshold);
            writer.WriteNumber("covered", covered);
            writer.WriteNumber("total", total);
            writer.WriteString("revision", revision);
            writer.WriteStartArray("parents");
            foreach (var parent in parents)
            {
                writer.WriteStringValue(parent);
            }

            writer.WriteEndArray();
            writer.WriteString("base", baseRevision);
            writer.WriteString("configurationSha256", configurationHash);
            writer.WriteStartArray("reports");
            foreach (var file in files)
            {
                writer.WriteStringValue(Path.GetRelativePath(root, file).Replace('\\', '/'));
            }

            writer.WriteEndArray();
            if (failure is not null)
            {
                writer.WriteString("error", failure);
            }

            writer.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        await File.WriteAllBytesAsync(destination, buffer.ToArray(), CancellationToken.None).ConfigureAwait(false);
    }
}
