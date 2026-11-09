using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// Checks the projects that a change affects without creating a Unix domain socket, for a sandbox that refuses them: whitespace in
/// folder mode, a build with node reuse off, and the built test executables run directly. It reports what ran and what could not.
/// </summary>
internal static partial class LocalCheck
{
    /// <summary>What the lanes cannot do without a socket; the report always lists it, so a green result is never read as complete.</summary>
    public static readonly string[] NotCovered =
    [
        "project-mode `dotnet format` style and analyzer rules, such as IDE0002, because the Roslyn build host uses a Unix domain socket; CI's `dotnet format --verify-no-changes` still checks them.",
        "coverage collection, because the coverlet host pipe is a Unix domain socket; the changed-code coverage gate needs a run where sockets are allowed.",
        "whitespace of source files that a project compiles from outside its own directory, such as `tools/Shared/*.cs`, because folder mode visits only project directories.",
        "`dotnet test` itself, so the test executables run directly and test projects that need a slice service, generated OpenAPI documents, or Playwright browsers are not run.",
    ];

    private const int MaxLines = 20;

    private const int MaxLineLength = 300;

    private const int MaxLogLength = 256 * 1024;

    private const string FormatFix = "dotnet format whitespace --folder ";

    /// <summary>Runs the lanes for the projects that the change since <see cref="LocalCheckOptions.Base"/> affects, prints the report, and fails when it must.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="selection">The directory that receives the plan, the solution filter, and the lane logs.</param>
    /// <param name="options">What to run and how.</param>
    /// <param name="run">Runs a child process and returns its standard output.</param>
    /// <param name="runConfigured">Runs a child process after its start information is adjusted.</param>
    /// <param name="output">Receives the selection reasons and the report.</param>
    /// <param name="error">Receives warnings of the selection.</param>
    /// <param name="ct">Cancels the check; the running child process tree is terminated first.</param>
    /// <param name="timeProvider">The clock that times the lanes; none uses the system clock.</param>
    /// <exception cref="InvalidOperationException">
    /// The root is not in a Git work tree, the base is not a commit, the change selects no project, a lane failed, no lane passed, or <see cref="LocalCheckOptions.Strict"/> is set and a lane could not run.
    /// </exception>
    public static async Task Run(
        string root,
        string selection,
        LocalCheckOptions options,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        Func<string, string[], string, Action<ProcessStartInfo>, CancellationToken, Task<string>> runConfigured,
        TextWriter output,
        TextWriter error,
        CancellationToken ct,
        TimeProvider? timeProvider = null)
    {
        var time = timeProvider ?? TimeProvider.System;
        string inside;
        try
        {
            inside = (await run("git", ["rev-parse", "--is-inside-work-tree"], root, ct).ConfigureAwait(false)).Trim();
        }
        catch (InvalidOperationException exception) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException("local-check needs a Git work tree, and `git rev-parse --is-inside-work-tree` failed: " + Bound(exception.Message), exception);
        }

        if (inside != "true")
        {
            throw new InvalidOperationException("local-check needs a Git work tree.");
        }

        string? baseId = null;
        if (options.Base is { } requested)
        {
            if (requested.StartsWith('-') || requested.Length == 0)
            {
                throw new InvalidOperationException("--base must be a revision, not an option.");
            }

            try
            {
                baseId = (await run("git", ["rev-parse", "--verify", "--quiet", requested + "^{commit}"], root, ct).ConfigureAwait(false)).Trim();
            }
            catch (InvalidOperationException exception) when (!ct.IsCancellationRequested)
            {
                throw new InvalidOperationException("--base " + ControlCharacters.Escape(requested) + " is not a commit of this checkout.", exception);
            }
        }

        var plan = await ProjectSelectionPlan.Write(root, selection, baseId, null, run, output, error, ct, time, includeWorkingTree: true).ConfigureAwait(false);
        var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
        var notRun = new List<string>();
        var tests = SelectTests(plan, configuration, notRun);
        string[] buildProjects = [.. plan.BuildProjects.Concat(tests).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        var selected = buildProjects.Concat(plan.Slices.Where(static slice => slice.Required).SelectMany(static slice => slice.Tests).Select(static test => test.Project))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (selected.Length == 0)
        {
            throw new InvalidOperationException("local-check selected no project, so it would check nothing. The change affects no solution project; use a revision that includes the change, or check the files another way.");
        }

        foreach (var project in selected)
        {
            RepositoryPath.Resolve(root, project);
        }

        var lanes = new List<LaneResult>();
        lanes.Add(await Lane("whitespace", () => Whitespace(selected)).ConfigureAwait(false));
        var buildLane = await Lane("build", () => Build(buildProjects.Concat(selected).Distinct(StringComparer.Ordinal).ToArray())).ConfigureAwait(false);
        lanes.Add(buildLane);
        lanes.Add(await Lane("tests", () => buildLane.Status == LaneStatus.Passed
            ? RunTests(tests)
            : Task.FromResult((LaneStatus.Skipped, "the build lane did not pass, so there are no test executables to run."))).ConfigureAwait(false));

        await PrintReport(output, plan, options, lanes, notRun).ConfigureAwait(false);
        var failed = lanes.Where(static lane => lane.Status == LaneStatus.Failed).Select(static lane => lane.Name).ToArray();
        if (failed.Length > 0)
        {
            throw new InvalidOperationException("local-check failed: " + string.Join(", ", failed) + ".");
        }

        if (lanes.All(static lane => lane.Status != LaneStatus.Passed))
        {
            throw new InvalidOperationException("local-check checked nothing: no lane passed.");
        }

        var skipped = lanes.Where(static lane => lane.Status == LaneStatus.Skipped).Select(static lane => lane.Name).Concat(notRun.Count > 0 ? ["not run: " + notRun.Count.ToString(CultureInfo.InvariantCulture)] : []).ToArray();
        if (options.Strict && skipped.Length > 0)
        {
            throw new InvalidOperationException("local-check --strict failed because something could not run: " + string.Join(", ", skipped) + ".");
        }

        async Task<LaneResult> Lane(string name, Func<Task<(LaneStatus Status, string Detail)>> action)
        {
            var timestamp = time.GetTimestamp();
            var (status, detail) = await action().ConfigureAwait(false);
            var result = new LaneResult(name, status, detail, time.GetElapsedTime(timestamp));
            var log = detail.Length > MaxLogLength ? detail[^MaxLogLength..] : detail;
            await File.WriteAllTextAsync(Path.Combine(selection, "local-check-" + name + ".log"), log, CancellationToken.None).ConfigureAwait(false);
            return result;
        }

        async Task<(LaneStatus Status, string Detail)> Whitespace(string[] projects)
        {
            var directories = Directories(projects);
            var wrong = new SortedSet<string>(StringComparer.Ordinal);
            var other = new List<string>();
            foreach (var directory in directories)
            {
                var path = Path.GetFullPath(directory, root);
                var (status, text) = await Attempt("dotnet", ["format", "whitespace", "--folder", path, "--verify-no-changes", "--exclude", "bin/", "obj/"], root).ConfigureAwait(false);
                if (status == LaneStatus.Passed)
                {
                    continue;
                }

                if (status == LaneStatus.Skipped)
                {
                    return (LaneStatus.Skipped, text);
                }

                var files = WhitespaceFinding().Matches(text).Select(static match => match.Groups["file"].Value).Distinct(StringComparer.Ordinal).ToArray();
                if (files.Length == 0)
                {
                    other.Add(directory + ": " + Describe(text));
                    continue;
                }

                foreach (var file in files)
                {
                    wrong.Add(Path.GetRelativePath(root, file).Replace('\\', '/') + " (fix: `" + FormatFix + directory + "`)");
                }
            }

            if (wrong.Count == 0 && other.Count == 0)
            {
                return (LaneStatus.Passed, Count(directories.Length, "directory", "directories") + " in folder mode.");
            }

            var detail = new StringBuilder();
            if (wrong.Count > 0)
            {
                detail.Append("wrong whitespace in ").Append(Count(wrong.Count, "file", "files")).Append(": ").Append(string.Join("; ", wrong.Take(MaxLines)));
                detail.Append(wrong.Count > MaxLines ? "; and " + (wrong.Count - MaxLines).ToString(CultureInfo.InvariantCulture) + " more." : ".");
            }

            foreach (var line in other)
            {
                detail.Append(detail.Length > 0 ? " " : string.Empty).Append(line);
            }

            return (LaneStatus.Failed, ControlCharacters.Escape(detail.ToString()));
        }

        async Task<(LaneStatus Status, string Detail)> Build(string[] projects)
        {
            var filter = Path.Combine(selection, "local-check.slnf");
            await SolutionFilter.Write(root, configuration.Solution, filter, projects, ct).ConfigureAwait(false);
            string[] nodeReuse = ["-m:1", "-nodeReuse:false"];
            var (restore, restoreText) = await Attempt("dotnet", ["restore", filter, "--locked-mode", .. nodeReuse], root).ConfigureAwait(false);
            if (restore != LaneStatus.Passed)
            {
                return (restore, "restore --locked-mode: " + restoreText);
            }

            var (build, buildText) = await Attempt("dotnet", ["build", filter, "-c", options.Configuration, "--no-restore", .. nodeReuse], root).ConfigureAwait(false);
            return build == LaneStatus.Passed
                ? (LaneStatus.Passed, "restore --locked-mode and build of " + Count(projects.Length, "project", "projects") + " with node reuse off.")
                : (build, "build: " + buildText);
        }

        async Task<(LaneStatus Status, string Detail)> RunTests(string[] projects)
        {
            if (projects.Length == 0)
            {
                return (LaneStatus.Skipped, notRun.Count > 0 ? "every selected test project needs something this lane cannot start; see the list below." : "no test project is selected.");
            }

            var passed = new List<string>();
            var failures = new List<string>();
            foreach (var project in projects)
            {
                var name = Path.GetFileNameWithoutExtension(project);
                var projectPath = Path.GetFullPath(project, root);
                var (evaluation, target) = await Attempt("dotnet", ["msbuild", projectPath, "-getProperty:TargetPath", "-p:Configuration=" + options.Configuration, "-m:1", "-nodeReuse:false"], root).ConfigureAwait(false);
                if (evaluation == LaneStatus.Passed && target.Trim().Length == 0)
                {
                    notRun.Add(name + ": multi-target project: not supported, set a single target (its TargetPath is empty).");
                    continue;
                }

                var library = evaluation == LaneStatus.Passed ? Path.GetFullPath(target.Trim(), Path.GetDirectoryName(projectPath)!) : string.Empty;
                if (evaluation != LaneStatus.Passed || !File.Exists(library))
                {
                    if (evaluation == LaneStatus.Failed)
                    {
                        failures.Add(name + ": " + target);
                    }
                    else
                    {
                        notRun.Add(name + ": the built test assembly was not found" + (library.Length == 0 ? "." : " at " + ControlCharacters.Escape(library) + "."));
                    }

                    continue;
                }

                var host = Path.ChangeExtension(library, OperatingSystem.IsWindows() ? ".exe" : null);
                string[] platform = ["--progress", "off", .. options.TestArguments];
                var (status, text) = File.Exists(host)
                    ? await Attempt(host, platform, Path.GetDirectoryName(library)!).ConfigureAwait(false)
                    : await Attempt("dotnet", [library, .. platform], Path.GetDirectoryName(library)!).ConfigureAwait(false);
                switch (status)
                {
                    case LaneStatus.Passed:
                        passed.Add(name);
                        break;
                    case LaneStatus.Failed:
                        failures.Add(name + ": " + text);
                        break;
                    default:
                        notRun.Add(name + ": " + text);
                        break;
                }
            }

            if (failures.Count > 0)
            {
                return (LaneStatus.Failed, string.Join(" ", failures));
            }

            return passed.Count == 0
                ? (LaneStatus.Skipped, "no test executable could run; see the list below.")
                : (LaneStatus.Passed, Count(passed.Count, "test project", "test projects") + " run directly with `--progress off`: " + string.Join(", ", passed) + ".");
        }

        // A missing tool skips the lane, a refused socket and any other failure fail it, and the text is bounded and escaped either way.
        async Task<(LaneStatus Status, string Text)> Attempt(string fileName, string[] arguments, string directory)
        {
            using var timeout = new CancellationTokenSource(options.Timeout, time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            try
            {
                return (LaneStatus.Passed, await runConfigured(fileName, arguments, directory, ApplyEnvironment, linked.Token).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // The runner terminates the process tree before it reports the cancellation.
                return (LaneStatus.Failed, ControlCharacters.Escape(Path.GetFileName(fileName)) + " timed out after " + options.Timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " seconds and its process tree was killed. Raise --timeout-minutes if it needs longer.");
            }
            catch (InvalidOperationException exception) when (!ct.IsCancellationRequested)
            {
                return exception.Message.StartsWith("Failed to start ", StringComparison.Ordinal)
                    ? (LaneStatus.Skipped, ControlCharacters.Escape(Path.GetFileName(fileName)) + " could not start, so it is not installed or not in an absolute PATH directory.")
                    : (LaneStatus.Failed, Describe(exception.Message));
            }
        }
    }

    /// <summary>Gives a child process the settings that keep MSBuild from starting worker nodes or a build server that need a socket.</summary>
    internal static void ApplyEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
    }

    private static string[] SelectTests(SelectionPlan plan, CiConfiguration configuration, List<string> notRun)
    {
        var tests = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var slice in plan.Slices.Where(static slice => slice.Required))
        {
            var configured = configuration.Slices.First(candidate => candidate.Name == slice.Name);
            var needs = configured.Service is not null ? "a service that `run` starts"
                : configured.OpenApi is { Length: > 0 } ? "generated OpenAPI documents"
                : configured.Playwright is not null ? "Playwright browsers"
                : null;
            foreach (var test in slice.Tests)
            {
                if (needs is null)
                {
                    tests.Add(test.Project);
                }
                else
                {
                    notRun.Add(Path.GetFileNameWithoutExtension(test.Project) + ": slice " + slice.Name + " needs " + needs + ", which `run` provides.");
                }
            }
        }

        return [.. tests];
    }

    private static string[] Directories(string[] projects)
    {
        var kept = new List<string>();
        foreach (var directory in projects
            .Select(static project => Path.GetDirectoryName(project)?.Replace('\\', '/') is { Length: > 0 } name ? name : ".")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            // An ancestor already covers its nested directories.
            if (!kept.Any(ancestor => ancestor == "." || directory.StartsWith(ancestor + "/", StringComparison.Ordinal)))
            {
                kept.Add(directory);
            }
        }

        return [.. kept];
    }

    private static async Task PrintReport(TextWriter output, SelectionPlan plan, LocalCheckOptions options, List<LaneResult> lanes, List<string> notRun)
    {
        var report = new StringBuilder();
        report.Append("local-check report: ")
            .Append(plan.ComparisonBase is null ? "no base revision" : "base " + plan.ComparisonBase[..Math.Min(12, plan.ComparisonBase.Length)])
            .Append(", configuration ").Append(ControlCharacters.Escape(options.Configuration))
            .Append(options.Strict ? ", strict" : string.Empty).Append(", working tree included.").Append('\n');
        foreach (var lane in lanes)
        {
            report.Append("  ").Append(lane.Name.PadRight(11)).Append(lane.Status switch
            {
                LaneStatus.Passed => "passed   ",
                LaneStatus.Failed => "FAILED   ",
                _ => "SKIPPED  ",
            }).Append(string.Create(CultureInfo.InvariantCulture, $"({lane.Duration.TotalSeconds:F1}s) ")).Append(lane.Detail).Append('\n');
        }

        if (notRun.Count > 0)
        {
            report.Append("Selected but not run:\n");
            foreach (var item in notRun)
            {
                report.Append("  - ").Append(ControlCharacters.Escape(item)).Append('\n');
            }
        }

        report.Append("This lane does not check:\n");
        foreach (var item in NotCovered)
        {
            report.Append("  - ").Append(item).Append('\n');
        }

        var failed = lanes.Any(static lane => lane.Status == LaneStatus.Failed);
        var nonePassed = lanes.All(static lane => lane.Status != LaneStatus.Passed);
        var incomplete = lanes.Any(static lane => lane.Status == LaneStatus.Skipped) || notRun.Count > 0;
        report.Append("Result: ").Append(failed ? "FAILED." : nonePassed ? "FAILED. No lane passed, so nothing was checked." : "the lanes that ran passed" + (incomplete ? ", but some could not run" : string.Empty) + "; this is not the whole of CI.").Append('\n');
        await output.WriteAsync(report.ToString()).ConfigureAwait(false);
    }

    private static string Describe(string text)
    {
        if (SocketRefusal.IsRefused(text))
        {
            return SocketRefusal.Message;
        }

        var hint = text.StartsWith("Command failed (8)", StringComparison.Ordinal) ? "no test ran (exit code 8), so nothing was checked. " : string.Empty;
        return hint + Bound(text);
    }

    // Process output is untrusted text: keep the last lines, shorten each, and escape what could drive a terminal.
    private static string Bound(string text)
    {
        var lines = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kept = lines.TakeLast(MaxLines).Select(static line => ControlCharacters.Escape(line.Length > MaxLineLength ? line[..MaxLineLength] + "..." : line));
        return string.Join(" | ", kept);
    }

    private static string Count(int count, string singular, string plural) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} ") + (count == 1 ? singular : plural);

    [GeneratedRegex(@"^(?<file>.+?)\(\d+,\d+\): error WHITESPACE", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex WhitespaceFinding();
}
