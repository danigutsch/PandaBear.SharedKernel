using SharedKernel.IO;

namespace SharedKernel.CI;

/// <summary>Checks whether a comparison is confined to the reviewed package source exclusion.</summary>
internal static class CandidateApplicability
{
    private const string BenchmarkDirectory = "benchmarks/";
    private const string BenchmarkSourceExclusion = BenchmarkDirectory + "**/*.cs";

    /// <summary>Returns whether the comparison still requires building a package candidate.</summary>
    internal static async Task<bool> RequiresCandidate(
        string root, string exclusion, SelectionPlan selection, string[]? changes, Func<string[], Task<string>> git, CancellationToken ct)
    {
        if (exclusion != BenchmarkSourceExclusion || !selection.BuildRequired || selection.FullValidation
            || !ProjectSelectionPlan.IsObjectId(selection.Revision) || selection.ComparisonBase is not { } baseRevision
            || changes is not { Length: > 0 } || !changes.All(IsExcludedSource)
            || selection.Slices.Any(static slice => slice.Required)
            || !selection.BuildProjects.All(static project => project.StartsWith(BenchmarkDirectory, StringComparison.Ordinal)))
        {
            return true;
        }

        try
        {
            var raw = await git([
                "diff", "--raw", "--no-abbrev", "--no-renames", "--no-ext-diff", "--no-color", "--ignore-submodules=none", "-z",
                baseRevision + "..." + selection.Revision, "--",
            ]).ConfigureAwait(false);
            if (!IsRegularSourceComparison(root, raw, changes))
            {
                return true;
            }

            var status = await git(["--no-optional-locks", "status", "--porcelain=v1", "--untracked-files=normal", "--ignore-submodules=none", "-z"])
                .ConfigureAwait(false);
            return status.Length != 0;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested
            && exception is InvalidOperationException or TimeoutException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return true;
        }
    }

    private static bool IsExcludedSource(string path) =>
        path.StartsWith(BenchmarkDirectory, StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal);

    private static bool IsRegularSourceComparison(string root, string raw, string[] changes)
    {
        var records = raw.Split('\0');
        if (records.Length != (changes.Length * 2) + 1 || records[^1].Length != 0)
        {
            return false;
        }

        var remaining = changes.ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < records.Length - 1; index += 2)
        {
            var path = records[index + 1];
            if (!IsSameModeModification(records[index]) || !remaining.Remove(path) || !RepositoryPath.TryParse(path, out var source))
            {
                return false;
            }

            var attributes = File.GetAttributes(source.ResolveUnder(root));
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
            {
                return false;
            }
        }

        return remaining.Count == 0;
    }

    private static bool IsSameModeModification(string header) =>
        header.Split(' ') is [var oldMode, var newMode, var oldId, var newId, "M"]
        && (newMode is "100644" or "100755") && oldMode == ":" + newMode
        && oldId.Length == newId.Length && oldId != newId
        && IsExistingObjectId(oldId) && IsExistingObjectId(newId);

    private static bool IsExistingObjectId(string value) =>
        ProjectSelectionPlan.IsObjectId(value) && value.Any(static digit => digit != '0');
}
