using System.Diagnostics;
using System.Text;
using PandaBear.IO;
using PandaBear.Processes;

namespace PandaBear.CI;

/// <summary>
/// Restores, builds, and tests the selected test projects of one or more slices through one solution filter, and records
/// the phases, a manifest, and diagnostics whether or not the run succeeds.
/// </summary>
internal static class SliceRun
{
    /// <summary>The filter that a run writes in its execution directory.</summary>
    public const string FilterFileName = "tests.slnf";

    /// <summary>The filter that a run restores and builds instead when a slice's Playwright project is not a selected test project.</summary>
    public const string BuildFilterFileName = "build.slnf";

    private const string Redaction = "[redacted]";

    /// <summary>Runs the selected test slices and writes their results.</summary>
    /// <exception cref="InvalidOperationException">No Cobertura report exists for a selected slice, or a service failed to stop.</exception>
    public static async Task Run(
        string root,
        string selection,
        SliceRunOptions options,
        IStepSummary? summary,
        Func<string[], string, CancellationToken, Task<string>> runGit,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        Func<string, string[], string, Action<ProcessStartInfo>, CancellationToken, Task<string>> runConfigured,
        TextWriter output,
        TextWriter error,
        CancellationToken ct,
        TimeProvider? timeProvider = null,
        Func<ProcessStartInfo, Process>? startService = null,
        Func<string, string[], string, CancellationToken, Task<string>>? runInstaller = null)
    {
        var time = timeProvider ?? TimeProvider.System;
        var start = startService ?? ProcessTree.Start;
        var execution = Path.Combine(selection, options.Slices.Count == 0 ? "execution" : "execution-" + string.Join('+', options.Slices));
        var results = Path.Combine(execution, "results");
        Directory.CreateDirectory(execution);
        var phases = new List<RunPhase>();
        var hosts = new List<ServiceHost>();
        var handed = new List<(string Variable, string Value)>();
        var started = time.GetUtcNow();
        var revision = "unavailable";
        var slices = options.Slices;
        string[] projects = [];
        var status = "failed";
        string? failure = null;
        string? teardownFailure = null;
        try
        {
            CiConfiguration configuration = null!;
            SliceConfiguration[] configured = [];
            await Phase("load", async () =>
            {
                configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
                var plan = await SelectionPlanFile.Read(selection, ct).ConfigureAwait(false);
                revision = (await run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
                if (plan.Revision != revision)
                {
                    throw new ArgumentException("Selection revision does not match the checkout revision.");
                }

                var planned = slices.Count == 0
                    ? plan.Slices.Where(static slice => slice.Required).ToArray()
                    : [.. slices.Select(name => plan.Slices.FirstOrDefault(slice => slice.Name == name) ?? throw new ArgumentException("Unknown slice: " + name + "."))];
                slices = [.. planned.Select(static slice => slice.Name)];
                configured = [.. planned.Where(static slice => slice.Required).Select(slice =>
                    configuration.Slices.FirstOrDefault(candidate => candidate.Name == slice.Name) ?? throw new ArgumentException("Unknown slice: " + slice.Name + "."))];
                projects = [.. planned.SelectMany(static slice => slice.Tests).Select(static test => test.Project).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

                // The plan can come from another job, so it may not point a build outside the checkout.
                if (projects.FirstOrDefault(static project => !RepositoryPath.IsValid(project)) is { } outside)
                {
                    throw new InvalidDataException(SelectionPlanFile.FileName + " names a test project outside the checkout: " + outside);
                }

                foreach (var project in projects)
                {
                    RepositoryPath.Parse(project).ResolveUnder(root);
                }

                return string.Empty;
            }).ConfigureAwait(false);

            if (projects.Length == 0)
            {
                await output.WriteLineAsync("No test projects selected for " + (slices.Count == 0 ? "the selection" : string.Join(", ", slices))).ConfigureAwait(false);
                status = "skipped";
                return;
            }

            string? sourceTree = null;
            if (options.CoverageEvidence)
            {
                await Phase("coverage-source", async () =>
                {
                    sourceTree = await CoverageReportEvidence.ReadTree(root, runGit, ct).ConfigureAwait(false);
                    return sourceTree;
                }).ConfigureAwait(false);
            }

            var targets = configuration.OpenApi ?? [];
            var openApi = configured.SelectMany(static slice => slice.OpenApi ?? []).Distinct(StringComparer.Ordinal)
                .Select(name => targets.First(candidate => candidate.Name == name))
                .ToArray();

            (string Slice, ServiceConfiguration Service)[] services =
            [
                .. configured.Where(static slice => slice.Service is not null).Select(static slice => (slice.Name, slice.Service!)),
            ];
            foreach (var (_, service) in services)
            {
                RepositoryPath.Parse(service.Project).ResolveUnder(root);
            }

            // Generation builds each target with --no-restore, and a service starts with --no-build --no-restore, so the filter restores and builds them too.
            var filter = Path.Combine(execution, FilterFileName);
            string[] filtered =
            [
                .. projects
                    .Concat(openApi.Select(static target => target.Project))
                    .Concat(services.Select(static service => service.Service.Project))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ];
            await SolutionFilter.Write(root, configuration.Solution, filter, filtered, ct).ConfigureAwait(false);
            PlaywrightConfiguration[] playwright = options.InstallPlaywright ? [.. configured.Select(static slice => slice.Playwright).OfType<PlaywrightConfiguration>()] : [];
            string[] built = [.. filtered.Union(playwright.Select(static install => install.Project), StringComparer.Ordinal)];
            var buildFilter = built.Length == filtered.Length ? filter : Path.Combine(execution, BuildFilterFileName);
            File.Delete(Path.Combine(execution, BuildFilterFileName));
            if (built.Length > filtered.Length)
            {
                await SolutionFilter.Write(root, configuration.Solution, buildFilter, built, ct).ConfigureAwait(false);
            }

            await Phase("restore", () => run("dotnet", ["restore", buildFilter, "--locked-mode"], root, ct)).ConfigureAwait(false);
            string[] build = ["build", buildFilter, "-c", options.Configuration, "--no-restore"];
            if (sourceTree is not null)
            {
                build = [.. build, "--no-incremental"];
            }

            await Phase("build", () => run("dotnet", build, root, ct)).ConfigureAwait(false);
            foreach (var target in openApi)
            {
                var name = target.Name;
                await Phase("openapi-" + name, () => runConfigured(
                    OpenApiGeneration.DotnetHostPath(), OpenApiGeneration.Arguments(target, false, options.Configuration), root, startInfo => OpenApiGeneration.ApplyEnvironment(startInfo, target.PreserveEnvironment), ct))
                    .ConfigureAwait(false);
            }

            if (playwright.Length > 0)
            {
                await Phase("playwright", async () =>
                {
                    var log = new StringBuilder();
                    foreach (var install in playwright)
                    {
                        log.Append(await PlaywrightInstallation.Run(
                            root, install.Project, install.Browsers, options.PlaywrightDependencies, options.Configuration, run, ct, onlyShell: install.OnlyShell, runInstaller: runInstaller).ConfigureAwait(false));
                    }

                    return log.ToString();
                }).ConfigureAwait(false);
            }

            foreach (var (slice, service) in services)
            {
                await Phase(ServiceHost.PhaseName(slice), async () =>
                {
                    var host = ServiceHost.Start(slice, service, options.Configuration, root, execution, start);
                    hosts.Add(host);
                    handed.AddRange(await host.WaitUntilReady(ct).ConfigureAwait(false));
                    return host.ReadyMessage();
                }).ConfigureAwait(false);
            }

            if (Directory.Exists(results))
            {
                // A report left by an earlier run must not satisfy the coverage check.
                Directory.Delete(results, recursive: true);
            }

            string[] test = ["test", "--solution", filter, "-c", options.Configuration, "--no-build", "--no-restore", "--results-directory", results];
            string[] platform = options.Coverage ? ["--coverlet", "--coverlet-output-format", "cobertura"] : [];
            platform = [.. platform, .. options.ExcludeTraits.SelectMany(static trait => new[] { "--filter-not-trait", trait })];
            if (options.Coverage)
            {
                test = [.. test, "--results-directory-layout", "per-module"];
            }

            if (platform.Length > 0)
            {
                test = [.. test, "--", .. platform];
            }

            await Phase("test", () => handed.Count == 0 ? run("dotnet", test, root, ct) : RunTests(test)).ConfigureAwait(false);
            if (options.Coverage)
            {
                await Phase("coverage", async () =>
                {
                    // The per-module layout gives every test project a <project>/<target-framework>_<runtime> directory.
                    var missing = projects.Where(project =>
                    {
                        var directory = Path.Combine(results, Path.GetFileNameWithoutExtension(project));
                        return !Directory.Exists(directory) || !Directory.EnumerateFiles(directory, CoberturaReport.FilePattern, SearchOption.AllDirectories).Any();
                    }).ToArray();
                    if (missing.Length != 0)
                    {
                        throw new InvalidOperationException("No Cobertura report for " + string.Join(", ", missing) + " under " + results + ".");
                    }

                    if (sourceTree is not null)
                    {
                        var currentTree = await CoverageReportEvidence.ReadTree(root, runGit, ct).ConfigureAwait(false);
                        if (currentTree != sourceTree)
                        {
                            throw new InvalidOperationException("The staged tree changed during the coverage run. Rebuild and collect fresh coverage reports.");
                        }

                        await CoverageReportEvidence.Record(results, sourceTree, ct).ConfigureAwait(false);
                    }

                    return string.Empty;
                }).ConfigureAwait(false);
            }

            status = "passed";
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            throw;
        }
        finally
        {
            // The services stop first, so their phases and logs are part of the evidence, and a failure to stop never replaces the failure that came first.
            teardownFailure = await StopServices().ConfigureAwait(false);
            if (teardownFailure is not null)
            {
                status = "failed";
                failure = failure is null ? teardownFailure : failure + Environment.NewLine + teardownFailure;
            }

            // Evidence is written even when the run fails or is cancelled, so the job can upload it. Program.cs gives a cancelled
            // run 90 seconds for the stop above and this.
            var name = slices.Count == 0 ? "selection" : string.Join('+', slices);
            await RunEvidence.Write(
                execution, name, revision, slices, projects, options, status, failure, started, time.GetUtcNow(), phases, summary).ConfigureAwait(false);
        }

        // Only a run that would otherwise have passed gets here, and a service that failed or was killed must not let it pass.
        if (teardownFailure is not null)
        {
            throw new InvalidOperationException(teardownFailure);
        }

        async Task<string?> StopServices()
        {
            string? failed = null;
            foreach (var host in Enumerable.Reverse(hosts))
            {
                try
                {
                    await Phase(ServiceHost.StopPhaseName(host.Slice), host.Stop).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ObjectDisposedException)
                {
                    failed = failed is null ? exception.Message : failed + Environment.NewLine + exception.Message;
                }
            }

            return failed;
        }

        // The services' values reach only the test run, and its output and failure message, which can repeat them, never do.
        async Task<string> RunTests(string[] arguments)
        {
            try
            {
                return Redact(await runConfigured(
                    "dotnet",
                    arguments,
                    root,
                    startInfo =>
                    {
                        foreach (var (variable, value) in handed)
                        {
                            startInfo.Environment[variable] = value;
                        }
                    },
                    ct).ConfigureAwait(false));
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(Redact(exception.Message));
            }
        }

        string Redact(string text) => handed.Aggregate(text, static (current, service) => current.Replace(service.Value, Redaction, StringComparison.Ordinal));

        async Task Phase(string name, Func<Task<string>> action)
        {
            var phaseStarted = time.GetUtcNow();
            var timestamp = time.GetTimestamp();
            var log = Path.Combine(execution, name + ".log");
            try
            {
                var result = await action().ConfigureAwait(false);
                await File.WriteAllTextAsync(log, result, CancellationToken.None).ConfigureAwait(false);

                // The restore, build, generation, installation, and test output is progress, so it goes to standard error.
                await error.WriteAsync(result).ConfigureAwait(false);
                phases.Add(new RunPhase(name, "passed", phaseStarted, time.GetElapsedTime(timestamp), name + ".log"));
            }
            catch (Exception exception)
            {
                await File.WriteAllTextAsync(log, exception.Message, CancellationToken.None).ConfigureAwait(false);
                phases.Add(new RunPhase(name, "failed", phaseStarted, time.GetElapsedTime(timestamp), name + ".log"));
                throw;
            }
        }
    }
}
