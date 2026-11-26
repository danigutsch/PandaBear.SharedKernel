using System.Globalization;
using System.Text;
using System.Text.Json;
using SharedKernel.InputNormalization;

namespace SharedKernel.CI;

/// <summary>Writes the validation plan for the test projects affected since a base revision.</summary>
internal static class ProjectSelectionPlan
{
    /// <summary>The solution filter containing the affected build and selected test projects.</summary>
    internal const string AffectedBuildFilterName = "affected.build.slnf";

    /// <summary>The solution filter containing affected projects and the repository quality build roots.</summary>
    internal const string QualityBuildFilterName = "quality.build.slnf";

    /// <summary>The NUL-delimited paths of a known partial comparison.</summary>
    internal const string ChangedPathsFileName = "changed-paths.nul";

    /// <summary>How long one Git command may run before selection gives up on the comparison.</summary>
    public static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Computes the test project selection for a revision range, writes the plan and its outputs, and sets them as run outputs when <paramref name="outputs"/> is given.</summary>
    /// <remarks>
    /// With <c>includeWorkingTree</c>, the changed paths also include the uncommitted changes of the working tree and its untracked,
    /// not ignored files, as a local check needs them; a failed listing selects every test project. The method returns the plan it wrote.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The configuration or solution is invalid, or a test slice conflicts with the additional candidate output.</exception>
    public static async Task<SelectionPlan> Write(
        string root,
        string directory,
        string? baseRevision,
        IRunOutputs? outputs,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter output,
        TextWriter error,
        CancellationToken ct,
        TimeProvider? timeProvider = null,
        bool includeWorkingTree = false,
        string? candidateExcludedSource = null)
    {
        var time = timeProvider ?? TimeProvider.System;
        var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
        var graph = SolutionGraph.Load(root, configuration.Solution, configuration.QualityBuild?.GovernedPackageIdPrefix);
        ProjectSelector.Validate(configuration, graph);
        if (outputs is not null && candidateExcludedSource is not null && configuration.Slices.Any(static slice => slice.Name == "candidate"))
        {
            throw CiConfigurationFile.Invalid("candidate_required is already used by the candidate test slice.");
        }

        string[]? changes = null;
        string? fullReason = null;
        if (string.IsNullOrEmpty(baseRevision))
        {
            fullReason = "no --base revision.";
        }
        else if (!IsObjectId(baseRevision))
        {
            fullReason = "--base is not a full hexadecimal Git object ID.";
        }
        else
        {
            try
            {
                await Git(["merge-base", "--is-ancestor", baseRevision, "HEAD"]).ConfigureAwait(false);
                try
                {
                    var diff = await Git(["diff", "--name-only", "--no-renames", "-z", baseRevision + "...HEAD", "--"]).ConfigureAwait(false);
                    changes = diff.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                }
                catch (Exception exception) when (!ct.IsCancellationRequested && exception is InvalidOperationException or TimeoutException)
                {
                    fullReason = "git diff " + baseRevision + "...HEAD failed or did not finish within " + Seconds(GitTimeout) + " seconds.";
                }
            }
            catch (Exception exception) when (!ct.IsCancellationRequested && exception is InvalidOperationException or TimeoutException)
            {
                fullReason = "--base " + baseRevision + " is not an ancestor of HEAD in this checkout.";
            }
        }

        if (includeWorkingTree && changes is not null)
        {
            try
            {
                var tracked = await Git(["diff", "--name-only", "--no-renames", "-z", "HEAD", "--"]).ConfigureAwait(false);
                var untracked = await Git(["ls-files", "-z", "--others", "--exclude-standard"]).ConfigureAwait(false);
                changes = [.. changes.Concat(tracked.Split('\0', StringSplitOptions.RemoveEmptyEntries)).Concat(untracked.Split('\0', StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal)];
            }
            catch (Exception exception) when (!ct.IsCancellationRequested && exception is InvalidOperationException or TimeoutException)
            {
                changes = null;
                fullReason = "the uncommitted changes of the working tree could not be listed.";
            }
        }

        string? nonInert = null;
        if (changes is not null && changes.Any(path => ProjectSelector.Match(configuration.Selection.Documentation, path) is not null))
        {
            try
            {
                var files = await Git(["ls-files", "-z", "--", .. BuildFileInertness.Pathspecs]).ConfigureAwait(false);
                nonInert = await BuildFileInertness.FindNonInert(root, files.Split('\0', StringSplitOptions.RemoveEmptyEntries), ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested && exception is InvalidOperationException or TimeoutException)
            {
                nonInert = "git ls-files could not list the tracked build files.";
            }
        }

        var selection = ProjectSelector.Select(configuration, graph, changes, fullReason, nonInert);
        var revision = "unavailable";
        try
        {
            revision = (await Git(["rev-parse", "HEAD"]).ConfigureAwait(false)).Trim();
        }
        catch (Exception exception) when (!ct.IsCancellationRequested && exception is InvalidOperationException or TimeoutException)
        {
            await error.WriteLineAsync("Selection revision unavailable; validation requires a Git checkout.").ConfigureAwait(false);
        }

        selection = selection with
        {
            Revision = revision,
            ComparisonBase = baseRevision
        };
        var candidateRequired = outputs is null || candidateExcludedSource is null || includeWorkingTree
            || await CandidateApplicability.RequiresCandidate(root, candidateExcludedSource, selection, changes, Git, ct).ConfigureAwait(false);
        Directory.CreateDirectory(directory);

        // A filter left by an earlier plan in the same directory must not look selected.
        foreach (var stale in Directory.EnumerateFiles(directory, "*.slnf"))
        {
            File.Delete(stale);
        }

        await SelectionPlanFile.Write(selection, directory, ct).ConfigureAwait(false);
        if (selection.BuildProjects.Count > 0)
        {
            await SolutionFilter.Write(root, configuration.Solution, Path.Combine(directory, CiConfigurationFile.BuildFilterName + ".slnf"), selection.BuildProjects, ct)
                .ConfigureAwait(false);
        }

        foreach (var slice in selection.Slices.Where(static slice => slice.Required))
        {
            await SolutionFilter.Write(root, configuration.Solution, Path.Combine(directory, slice.Name + ".slnf"), slice.Tests.Select(static test => test.Project), ct)
                .ConfigureAwait(false);
        }

        var changedPaths = Path.Combine(directory, ChangedPathsFileName);
        File.Delete(changedPaths);
        if (changes is not null && selection.BuildRequired && !selection.FullValidation)
        {
            var affected = selection.BuildProjects.Concat(selection.Slices.SelectMany(static slice => slice.Tests)
                .Select(static test => test.Project));
            await SolutionFilter.Write(root, configuration.Solution, Path.Combine(directory, AffectedBuildFilterName), affected, ct)
                .ConfigureAwait(false);
            if (configuration.QualityBuild is { } qualityBuild)
            {
                await SolutionFilter.Write(root, configuration.Solution, Path.Combine(directory, QualityBuildFilterName),
                    affected.Concat(graph.GovernedProjects).Concat(qualityBuild.Projects).Distinct(StringComparer.Ordinal), ct).ConfigureAwait(false);
            }

            var paths = changes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            await File.WriteAllTextAsync(changedPaths, string.Join('\0', paths) + '\0', ct).ConfigureAwait(false);
        }

        if (outputs is not null)
        {
            List<KeyValuePair<string, string>> values =
            [
                new("build_required", Bool(selection.BuildRequired)),
                new("documentation_only", Bool(selection.DocumentationOnly)),
                new("full_validation", Bool(selection.FullValidation)),
                new("matrix", Matrix(selection.Slices)),
            ];
            foreach (var slice in selection.Slices)
            {
                values.Add(new(slice.Name + "_required", Bool(slice.Required)));
            }

            if (candidateExcludedSource is not null)
            {
                values.Add(new("candidate_required", Bool(candidateRequired)));
            }

            await outputs.Write(values, ct).ConfigureAwait(false);
        }

        // Changed paths come from the pull request, so each reason line is escaped.
        foreach (var reason in selection.Reasons)
        {
            await output.WriteLineAsync(ControlCharacters.Escape(reason)).ConfigureAwait(false);
        }

        return selection;

        async Task<string> Git(string[] arguments)
        {
            using var timeout = new CancellationTokenSource(GitTimeout, time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            try
            {
                return await run("git", arguments, root, linked.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException("git " + arguments[0] + " did not finish within " + Seconds(GitTimeout) + " seconds.", exception);
            }
        }
    }

    /// <summary>Reports whether <paramref name="value"/> is a full hexadecimal SHA-1 or SHA-256 Git object ID.</summary>
    public static bool IsObjectId(string value) =>
        value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit);

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Seconds(TimeSpan value) => value.TotalSeconds.ToString(CultureInfo.InvariantCulture);

    private static string Matrix(IReadOnlyList<SliceSelection> slices)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var slice in slices.Where(static slice => slice.Required))
            {
                writer.WriteStringValue(slice.Name);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
