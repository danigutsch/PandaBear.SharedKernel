using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// Restores, builds, and tests the selected test projects of one or more slices through one solution filter, and records
/// the phases, a manifest, and diagnostics whether or not the run succeeds.
/// </summary>
internal static class SliceRun
{
    /// <summary>The filter that a run writes in its execution directory.</summary>
    public const string FilterFileName = "tests.slnf";

    private const int MaxPlanBytes = 16 * 1024 * 1024;

    private const string Redaction = "[redacted]";

    public static async Task Run(
        string root,
        string selection,
        SliceRunOptions options,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        Func<string, string[], string, Action<ProcessStartInfo>, CancellationToken, Task<string>> runConfigured,
        TextWriter output,
        TextWriter error,
        CancellationToken ct,
        TimeProvider? timeProvider = null,
        Func<ProcessStartInfo, Process>? startService = null)
    {
        var time = timeProvider ?? TimeProvider.System;
        var start = startService ?? ProcessRunner.Start;
        var execution = Path.Combine(selection, options.Slices.Length == 0 ? "execution" : "execution-" + string.Join('+', options.Slices));
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
                var plan = JsonSerializer.Deserialize(
                    await BoundedFile.Read(Path.Combine(selection, ProjectSelectionPlan.PlanFileName), MaxPlanBytes, ProjectSelectionPlan.PlanFileName, ct).ConfigureAwait(false),
                    SelectionPlanJsonContext.Default.SelectionPlan) ?? throw new InvalidDataException(ProjectSelectionPlan.PlanFileName + " must contain a JSON object.");
                revision = (await run("git", ["rev-parse", "HEAD"], root, ct).ConfigureAwait(false)).Trim();
                if (plan.Revision != revision)
                {
                    throw new ArgumentException("Selection revision does not match the checkout revision.");
                }

                var planned = slices.Length == 0
                    ? plan.Slices.Where(static slice => slice.Required).ToArray()
                    : [.. slices.Select(name => plan.Slices.FirstOrDefault(slice => slice.Name == name) ?? throw new ArgumentException("Unknown slice: " + name + "."))];
                slices = [.. planned.Select(static slice => slice.Name)];
                configured = [.. planned.Where(static slice => slice.Required).Select(slice =>
                    configuration.Slices.FirstOrDefault(candidate => candidate.Name == slice.Name) ?? throw new ArgumentException("Unknown slice: " + slice.Name + "."))];
                projects = [.. planned.SelectMany(static slice => slice.Tests).Select(static test => test.Project).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

                // The plan can come from another job, so it may not point a build outside the checkout.
                if (projects.FirstOrDefault(static project => !RepositoryPath.IsRelative(project)) is { } outside)
                {
                    throw new InvalidDataException(ProjectSelectionPlan.PlanFileName + " names a test project outside the checkout: " + outside);
                }

                foreach (var project in projects)
                {
                    RepositoryPath.Resolve(root, project);
                }

                return string.Empty;
            }).ConfigureAwait(false);

            if (projects.Length == 0)
            {
                await output.WriteLineAsync("No test projects selected for " + (slices.Length == 0 ? "the selection" : string.Join(", ", slices))).ConfigureAwait(false);
                status = "skipped";
                return;
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
                RepositoryPath.Resolve(root, service.Project);
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
            await Phase("restore", () => run("dotnet", ["restore", filter, "--locked-mode"], root, ct)).ConfigureAwait(false);
            await Phase("build", () => run("dotnet", ["build", filter, "-c", options.Configuration, "--no-restore"], root, ct)).ConfigureAwait(false);
            foreach (var target in openApi)
            {
                var name = target.Name;
                await Phase("openapi-" + name, () => runConfigured(
                    OpenApiGeneration.DotnetHostPath(), OpenApiGeneration.Arguments(target, false, options.Configuration), root, OpenApiGeneration.ApplyEnvironment, ct))
                    .ConfigureAwait(false);
            }

            var playwright = configured.Select(static slice => slice.Playwright).OfType<PlaywrightConfiguration>()
                .Where(install => projects.Contains(install.Project, StringComparer.Ordinal))
                .ToArray();
            if (options.InstallPlaywright && playwright.Length > 0)
            {
                await Phase("playwright", async () =>
                {
                    var log = new StringBuilder();
                    foreach (var install in playwright)
                    {
                        log.Append(await PlaywrightInstallation.Run(
                            root, install.Project, install.Browsers, options.PlaywrightDependencies, options.Configuration, run, ct, onlyShell: install.OnlyShell).ConfigureAwait(false));
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
                    handed.Add((service.ValueVariable, await host.WaitUntilReady(ct).ConfigureAwait(false)));
                    return host.ReadyMessage();
                }).ConfigureAwait(false);
            }

            var results = Path.Combine(execution, "results");
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
                await Phase("coverage", () =>
                {
                    // The per-module layout gives every test project a <project>/<target-framework>_<runtime> directory.
                    var missing = projects.Where(project =>
                    {
                        var directory = Path.Combine(results, Path.GetFileNameWithoutExtension(project));
                        return !Directory.Exists(directory) || !Directory.EnumerateFiles(directory, CoberturaReport.FilePattern, SearchOption.AllDirectories).Any();
                    }).ToArray();
                    return missing.Length == 0
                        ? Task.FromResult(string.Empty)
                        : throw new InvalidOperationException("No Cobertura report for " + string.Join(", ", missing) + " under " + results + ".");
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
            var name = slices.Length == 0 ? "selection" : string.Join('+', slices);
            await RunEvidence.Write(
                execution, name, revision, slices, projects, options, status, failure, started, time.GetUtcNow(), phases, root).ConfigureAwait(false);
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
