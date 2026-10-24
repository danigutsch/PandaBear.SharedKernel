using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace PandaBear.CI.Tool;

/// <summary>Restores, builds, and tests the selected test projects of one slice through its solution filter.</summary>
internal static class RepositoryValidation
{
    private const int MaxPlanBytes = 16 * 1024 * 1024;

    public static async Task Run(
        string root,
        string selection,
        string slice,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter output,
        TextWriter error,
        CancellationToken ct)
    {
        var revision = (await run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
        var plan = JsonSerializer.Deserialize(
            await BoundedFile.Read(Path.Combine(selection, ProjectSelectionPlan.PlanFileName), MaxPlanBytes, ProjectSelectionPlan.PlanFileName, ct).ConfigureAwait(false),
            SelectionPlanJsonContext.Default.SelectionPlan) ?? throw new InvalidDataException(ProjectSelectionPlan.PlanFileName + " must contain a JSON object.");
        if (plan.Revision != revision)
        {
            throw new ArgumentException("Selection revision does not match the checkout revision.");
        }

        var selected = plan.Slices.FirstOrDefault(candidate => candidate.Name == slice) ?? throw new ArgumentException("Unknown slice.");
        if (!selected.Required)
        {
            await output.WriteLineAsync("No test projects selected for " + slice).ConfigureAwait(false);
            return;
        }

        var filter = Path.Combine(selection, slice + ".slnf");
        if (!File.Exists(filter))
        {
            throw new ArgumentException("The selection has no solution filter for the slice.");
        }

        var directory = Path.Combine(selection, "execution-" + slice);
        Directory.CreateDirectory(directory);
        var timer = Stopwatch.StartNew();
        var status = "failed";
        try
        {
            await Dotnet("restore", filter, "--locked-mode").ConfigureAwait(false);
            await Dotnet("build", filter, "-c", "Release", "--no-restore").ConfigureAwait(false);
            await Dotnet("test", "--solution", filter, "-c", "Release", "--no-build", "--no-restore").ConfigureAwait(false);
            status = "passed";
        }
        finally
        {
            // Record the outcome even when the run is cancelled.
            await File.WriteAllTextAsync(Path.Combine(directory, "timing.tsv"), "revision\tslice\tduration_seconds\tstatus\n" + revision + "\t" + slice + "\t"
                + timer.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture) + "\t" + status + "\n", CancellationToken.None).ConfigureAwait(false);
        }

        // The restore, build, and test output is progress, so it goes to standard error.
        async Task Dotnet(params string[] arguments) =>
            await error.WriteAsync(await run("dotnet", arguments, root, ct).ConfigureAwait(false)).ConfigureAwait(false);
    }
}
