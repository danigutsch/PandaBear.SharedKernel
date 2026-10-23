using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

namespace PandaBear.Repository.Tool;

internal static class RepositoryValidation
{
    public static async Task Run(
        string root,
        string selection,
        string group,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        TextWriter output,
        TextWriter error,
        CancellationToken ct)
    {
        var revision = (await run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
        var selectedRevision = await File.ReadAllTextAsync(Path.Combine(selection, "revision.txt"), ct).ConfigureAwait(false);
        if (selectedRevision.Trim() != revision)
        {
            throw new ArgumentException("Selection revision does not match the checkout revision.");
        }
        var known = XDocument.Load(Path.Combine(root, "SharedKernel.slnx")).Descendants("Project")
            .Select(project => (string)project.Attribute("Path")!).ToHashSet(StringComparer.Ordinal);
        var build = await File.ReadAllLinesAsync(Path.Combine(selection, "build-projects.txt"), ct).ConfigureAwait(false);
        var tests = await File.ReadAllLinesAsync(Path.Combine(selection, "test-projects.txt"), ct).ConfigureAwait(false);
        foreach (var project in build.Concat(tests))
        {
            if (!known.Contains(project) || Path.IsPathRooted(project) || project.Replace('\\', '/').Split('/').Contains("..", StringComparer.Ordinal))
            {
                throw new ArgumentException("Selection contains a project outside the live solution.");
            }
        }
        if (build.Distinct(StringComparer.Ordinal).Count() != build.Length || tests.Distinct(StringComparer.Ordinal).Count() != tests.Length
            || tests.Except(build, StringComparer.Ordinal).Any())
        {
            throw new ArgumentException("Selection must contain distinct projects and include every test build.");
        }
        var hosted = tests.Where(project => IsHosted(root, project)).ToArray();
        if (group != "all" && group != "ordinary" && !known.Contains(group))
        {
            throw new ArgumentException("Unknown test group.");
        }
        var selectedTests = group == "all" ? tests : group == "ordinary" ? tests.Except(hosted, StringComparer.Ordinal).ToArray() : tests.Where(project => project == group).ToArray();
        var selectedBuild = group == "all" ? build : group == "ordinary" ? build.Except(hosted, StringComparer.Ordinal).ToArray() : selectedTests;
        if (selectedBuild.Length == 0)
        {
            await output.WriteLineAsync("No projects selected for " + group).ConfigureAwait(false);
            return;
        }
        var directory = Path.Combine(selection, "execution-" + (group is "all" or "ordinary" ? group : Path.GetFileNameWithoutExtension(group)));
        Directory.CreateDirectory(directory);
        var graph = WriteSolution(root, directory, "build", selectedBuild);
        var timer = Stopwatch.StartNew();
        var status = "failed";
        try
        {
            await Dotnet("restore", graph, "--locked-mode").ConfigureAwait(false);
            await Dotnet("build", graph, "-c", "Release", "--no-restore").ConfigureAwait(false);
            var ordinaryTests = selectedTests.Except(hosted, StringComparer.Ordinal).ToArray();
            if (ordinaryTests.Length > 0)
            {
                var testGraph = WriteSolution(root, directory, "tests", ordinaryTests);
                await Dotnet("test", "--solution", testGraph, "-c", "Release", "--no-build", "--no-restore").ConfigureAwait(false);
            }
            foreach (var project in selectedTests.Intersect(hosted, StringComparer.Ordinal))
            {
                await Dotnet("test", "--project", project, "-c", "Release", "--no-build", "--no-restore").ConfigureAwait(false);
            }
            status = "passed";
        }
        finally
        {
            // Record the outcome even when the run is cancelled.
            await File.WriteAllTextAsync(Path.Combine(directory, "timing.tsv"), "revision\tgroup\tduration_seconds\tstatus\n" + revision + "\t" + group + "\t"
                + timer.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture) + "\t" + status + "\n", CancellationToken.None).ConfigureAwait(false);
        }

        // The restore, build, and test output is progress, so it goes to standard error.
        async Task Dotnet(params string[] arguments) =>
            await error.WriteAsync(await run("dotnet", arguments, root, ct).ConfigureAwait(false)).ConfigureAwait(false);
    }

    internal static bool IsHosted(string root, string project) =>
        XDocument.Load(Path.Combine(root, project)).Root?.Attribute("Sdk")?.Value == "Aspire.AppHost.Sdk";

    private static string WriteSolution(string root, string directory, string name, string[] projects)
    {
        var path = Path.Combine(directory, name + ".slnx");
        new XDocument(new XElement("Solution", projects.Select(project => new XElement("Project",
            new XAttribute("Path", Path.GetRelativePath(directory, Path.Combine(root, project))))))).Save(path);
        return path;
    }
}
