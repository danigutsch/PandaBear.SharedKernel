using System.Globalization;
using System.Text;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>Writes the validation plan for the test projects affected since a base revision.</summary>
internal static class ProjectSelectionPlan
{
    /// <summary>How long one Git command may run before selection gives up on the comparison.</summary>
    public static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The plan file in the selection directory.</summary>
    public const string PlanFileName = "selection.json";

    public static async Task Write(
        string root,
        string directory,
        string? baseRevision,
        string? githubOutput,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter output,
        TextWriter error,
        CancellationToken ct,
        TimeProvider? timeProvider = null)
    {
        var time = timeProvider ?? TimeProvider.System;
        var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
        var graph = SolutionGraph.Load(root, configuration.Solution);
        ProjectSelector.Validate(configuration, graph);

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
        Directory.CreateDirectory(directory);

        // A filter left by an earlier plan in the same directory must not look selected.
        foreach (var stale in Directory.EnumerateFiles(directory, "*.slnf"))
        {
            File.Delete(stale);
        }

        await File.WriteAllBytesAsync(
            Path.Combine(directory, PlanFileName), JsonSerializer.SerializeToUtf8Bytes(selection, SelectionPlanJsonContext.Default.SelectionPlan), ct).ConfigureAwait(false);
        if (selection.BuildProjects.Length > 0)
        {
            await SolutionFilter.Write(root, configuration.Solution, Path.Combine(directory, CiConfigurationFile.BuildFilterName + ".slnf"), selection.BuildProjects, ct)
                .ConfigureAwait(false);
        }

        foreach (var slice in selection.Slices.Where(static slice => slice.Required))
        {
            await SolutionFilter.Write(root, configuration.Solution, Path.Combine(directory, slice.Name + ".slnf"), slice.Tests.Select(static test => test.Project), ct)
                .ConfigureAwait(false);
        }

        if (githubOutput is not null)
        {
            var lines = new StringBuilder()
                .Append("build_required=").Append(Bool(selection.BuildRequired)).Append('\n')
                .Append("documentation_only=").Append(Bool(selection.DocumentationOnly)).Append('\n')
                .Append("matrix=").Append(Matrix(selection.Slices)).Append('\n');
            foreach (var slice in selection.Slices)
            {
                lines.Append(slice.Name).Append("_required=").Append(Bool(slice.Required)).Append('\n');
            }

            await File.AppendAllTextAsync(Path.GetFullPath(githubOutput, root), lines.ToString(), ct).ConfigureAwait(false);
        }

        // Changed paths come from the pull request, so each reason line is escaped.
        foreach (var reason in selection.Reasons)
        {
            await output.WriteLineAsync(ControlCharacters.Escape(reason)).ConfigureAwait(false);
        }

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

    private static string Matrix(SliceSelection[] slices)
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
