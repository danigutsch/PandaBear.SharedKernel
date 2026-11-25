using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharedKernel.IO;
using SharedKernel.Processes;

namespace SharedKernel.Versioning;

/// <summary>Compares the package API surface recorded by two candidate manifests.</summary>
internal static class CandidateApiComparison
{
    private const string Passed = "passed";
    private const string Differences = "differences";
    private const string Removed = "removed";
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    /// <summary>Writes a completed comparison and returns whether it found no differences.</summary>
    /// <exception cref="ArgumentException">A path or candidate identity is invalid.</exception>
    /// <exception cref="IOException">The new report cannot be published.</exception>
    internal static async Task<bool> Run(
        ExternalCommands commands, string baselineManifest, string candidateManifest, string apiCompatPath, string outputPath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselineManifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateManifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiCompatPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ct.ThrowIfCancellationRequested();
        var destination = Path.GetFullPath(outputPath);
        if (File.Exists(destination))
        {
            throw new IOException($"The API comparison report path already exists: {destination}.");
        }

        var scratch = Directory.CreateTempSubdirectory("sharedkernel-api-comparison-").FullName;
        JsonObject report;
        bool passed;
        try
        {
            var baseline = await CandidateArchiveSnapshot.Create(baselineManifest, Path.Combine(scratch, "baseline"),
                requireCoordinatedDependencies: true, ct).ConfigureAwait(false);
            var candidate = await CandidateArchiveSnapshot.Create(candidateManifest, Path.Combine(scratch, "candidate"),
                requireCoordinatedDependencies: true, ct).ConfigureAwait(false);
            var executable = Path.GetFullPath(apiCompatPath);
            var version = (await commands.Run(executable, ["--version"], scratch, ct).ConfigureAwait(false)).Trim();
            _ = SemanticVersion.Parse(version);

            var packages = await ComparePackages(commands, executable, scratch, baseline, candidate, ct).ConfigureAwait(false);
            passed = !packages.Any(static entry => entry?["status"]?.GetValue<string>() is Differences or Removed);
            report = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["status"] = passed ? Passed : Differences,
                ["baseline"] = baseline.Identity,
                ["candidate"] = candidate.Identity,
                ["apiCompatVersion"] = version,
                ["packages"] = packages,
            };
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }

        var bytes = Encoding.UTF8.GetBytes(report.ToJsonString(ReportOptions) + "\n");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!await AtomicFile.CreateIfAbsent(destination, (stream, token) => stream.WriteAsync(bytes.AsMemory(), token), ct).ConfigureAwait(false))
        {
            throw new IOException($"The API comparison report path already exists: {destination}.");
        }

        return passed;
    }

    private static async Task<JsonArray> ComparePackages(
        ExternalCommands commands, string executable, string workingDirectory,
        CandidateArchiveSnapshot baseline, CandidateArchiveSnapshot candidate, CancellationToken ct)
    {
        var report = new JsonArray();
        var ids = baseline.Packages.Keys.Union(candidate.Packages.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            baseline.Packages.TryGetValue(id, out var earlier);
            candidate.Packages.TryGetValue(id, out var current);
            var diagnostics = Array.Empty<string>();
            string status;
            if (earlier is null)
            {
                status = "added";
            }
            else if (current is null)
            {
                status = Removed;
            }
            else if (!HasAssemblyAssets(earlier))
            {
                status = "no-baseline-assembly";
            }
            else
            {
                var result = await commands.RunWithExitCode(executable,
                    ["package", current, "--baseline-package", earlier], workingDirectory, ct).ConfigureAwait(false);
                diagnostics = ReadDiagnostics(id, result);
                status = diagnostics.Length == 0 ? Passed : Differences;
            }

            JsonNode package = new JsonObject
            {
                ["packageId"] = id,
                ["baselinePackage"] = Path.GetFileName(earlier),
                ["candidatePackage"] = Path.GetFileName(current),
                ["status"] = status,
                ["diagnostics"] = new JsonArray([.. diagnostics.Select(static diagnostic => JsonValue.Create(diagnostic))]),
            };
            report.Add(package);
        }

        return report;
    }

    private static bool HasAssemblyAssets(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return archive.Entries.Any(static entry =>
            entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
            (entry.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) ||
             entry.FullName.StartsWith("ref/", StringComparison.OrdinalIgnoreCase) ||
             (entry.FullName.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase) &&
              entry.FullName.Contains("/lib/", StringComparison.OrdinalIgnoreCase))));
    }

    private static string[] ReadDiagnostics(string packageId, ProcessOutput result)
    {
        var lines = (result.StandardOutput + "\n" + result.StandardError).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var diagnostics = lines.Where(IsDiagnostic).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if ((result.ExitCode == 0 && diagnostics.Length == 0) || (result.ExitCode == 1 && diagnostics.Length != 0))
        {
            return diagnostics;
        }

        throw new InvalidOperationException(
            $"ApiCompat could not complete the comparison for {packageId} (exit {result.ExitCode}).{Environment.NewLine}{string.Join(Environment.NewLine, lines)}");
    }

    private static bool IsDiagnostic(string line)
    {
        var prefixLength = line.StartsWith("CP", StringComparison.Ordinal) ? 2
            : line.StartsWith("PKV", StringComparison.Ordinal) ? 3 : 0;
        return prefixLength > 0 && line.Length > prefixLength + 5 && line[prefixLength + 4] == ':' &&
            line.AsSpan(prefixLength, 4).IndexOfAnyExceptInRange('0', '9') < 0;
    }
}
