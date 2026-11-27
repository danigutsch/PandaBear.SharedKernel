using System.Diagnostics;
using SharedKernel.Processes;
using static SharedKernel.RepoConfig.Tool.ResourceBudgetGuard;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Runs the real outer guard over a child relay and a Podman wrapper backed by a scripted engine.</summary>
internal sealed class ResourceBudgetPodmanCompositionFixture : IAsyncDisposable
{
    /// <summary>The private readiness marker passed only to the launched fixture process.</summary>
    internal const string MarkerVariable = "SHAREDKERNEL_PODMAN_BUDGET_FIXTURE_MARKER";

    private const string RoleVariable = "SHAREDKERNEL_PODMAN_BUDGET_FIXTURE_ROLE";
    private const string FailCleanupVariable = "SHAREDKERNEL_PODMAN_BUDGET_FIXTURE_FAIL_CLEANUP";
    private const string CleanupBeforeStopVariable = "SHAREDKERNEL_PODMAN_BUDGET_FIXTURE_CLEANUP_BEFORE_STOP";
    private const string RejectedSuffix = ".next-rejected";
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);
    private readonly TemporaryDirectory _directory = new();
    private readonly Process _sibling;
    private readonly string _ready;
    private readonly bool _failCleanup;
    private readonly bool _cleanupBeforeStop;

    /// <summary>Creates the owned directory and a short-lived process outside the guarded command tree.</summary>
    internal ResourceBudgetPodmanCompositionFixture(bool failCleanup, bool cleanupBeforeStop)
    {
        _failCleanup = failCleanup;
        _cleanupBeforeStop = cleanupBeforeStop;
        _ready = Path.Combine(_directory.Path, "engine-ready");
        var runtime = Directory.CreateDirectory(Path.Combine(_directory.Path, "runtime"));
        CallerMarker = Path.Combine(runtime.FullName, "caller-owned");
        File.WriteAllText(CallerMarker, "retained");
        _sibling = ProcessTree.Start(new ProcessStartInfo("/bin/sleep") { UseShellExecute = false, ArgumentList = { "30" } });
    }

    /// <summary>Gets whether the process outside the guard's ownership is still running.</summary>
    internal bool SiblingIsRunning => !_sibling.HasExited;

    /// <summary>Gets the unrelated file in the caller-owned runtime parent.</summary>
    internal string CallerMarker { get; }

    /// <summary>Gets the proof that a serial successor was refused before engine creation.</summary>
    internal string NextEngineRejectedMarker => _ready + RejectedSuffix;

    /// <summary>Gets whether the guard retained the exact adverse engine receipt for diagnosis.</summary>
    internal bool HasRetainedCleanupReceipt =>
        Directory.EnumerateDirectories(_directory.Path, "sharedkernel-budget-abort-*")
            .Any(path => Directory.EnumerateFiles(path, "*.retained").Any());

    /// <summary>Advances deterministic samples only after the actual nested workload enters the private workspace.</summary>
    internal async Task<Report> TripBudget(Limits limits, Func<Sample> measure, bool cooperate, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(Deadline);
        return await Run(limits, measure, WaitForReady,
            token => ResourceBudgetProcess.Run(CreateWorker(_ready, "relay", _failCleanup, _cleanupBeforeStop), cooperate, token), deadline.Token);
    }

    /// <summary>Reads the private paths the nested wrapper reported immediately before the guard trip.</summary>
    internal async Task<(string Graph, string Runtime)> ReadVerifiedRoots(CancellationToken ct)
    {
        var roots = await File.ReadAllLinesAsync(_ready, ct);
        roots.Length.ShouldBe(2);
        Path.GetDirectoryName(roots[0]).ShouldBe(_directory.Path);
        Path.GetDirectoryName(roots[1]).ShouldBe(Path.GetDirectoryName(CallerMarker));
        return (roots[0], roots[1]);
    }

    /// <summary>Runs a relay or the nested Podman command, depending on which fixture process was launched.</summary>
    internal static async Task RunWorker(string marker, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(Deadline);
        var role = Environment.GetEnvironmentVariable(RoleVariable);
        var failCleanup = string.Equals(Environment.GetEnvironmentVariable(FailCleanupVariable), "1", StringComparison.Ordinal);
        var cleanupBeforeStop = string.Equals(Environment.GetEnvironmentVariable(CleanupBeforeStopVariable), "1", StringComparison.Ordinal);
        if (role == "relay")
        {
            var relay = new SystemProcessRunner();
            var first = await relay.Run(CreateWorker(marker, "engine", failCleanup, cleanupBeforeStop), forward: null, deadline.Token);
            first.ExitCode.ShouldBe(0, first.StandardError + first.StandardOutput);
            if (cleanupBeforeStop)
            {
                var control = Directory.GetDirectories(Path.GetDirectoryName(marker)!, "sharedkernel-budget-abort-*").ShouldHaveSingleItem();
                await WaitForMarker(Path.Combine(control, "stop"), deadline.Token);
            }
            else
            {
                var next = await relay.Run(CreateWorker(marker, "next", failCleanup, cleanupBeforeStop), forward: null, deadline.Token);
                next.ExitCode.ShouldBe(0, next.StandardError + next.StandardOutput);
            }
            return;
        }

        using var context = new IsolatedPodmanTestContext
        {
            DeleteRetainedRoots = false,
            FailOperation = failCleanup ? "rm --all --force --time 0" : null,
        };
        var runtimeParent = Path.Combine(Path.GetDirectoryName(marker)!, "runtime");
        if (role == "next")
        {
            var notLaunched = ScriptedProcessRunner.Throwing(new InvalidOperationException("A stopped recipe cannot start another engine."));
            var rejected = await context.RunCommand(["--runtime-parent", runtimeParent], [context.Executable], deadline.Token, notLaunched);
            rejected.ExitCode.ShouldBe(130, rejected.Error);
            context.Operations.ShouldBeEmpty();
            await File.WriteAllTextAsync(marker + RejectedSuffix, "not started", ct);
            return;
        }

        var pending = marker + ".pending";
        var runner = new ScriptedProcessRunner(async (_, _, token) =>
        {
            var graph = context.Roots.ShouldHaveSingleItem();
            var runtime = Path.GetDirectoryName(context.RunRoots.ShouldHaveSingleItem())!;
            Directory.Exists(graph).ShouldBeTrue();
            Directory.Exists(runtime).ShouldBeTrue();
            await File.WriteAllLinesAsync(pending, [graph, runtime], token);
            if (cleanupBeforeStop)
            {
                return new ProcessOutput(17, string.Empty);
            }

            File.Move(pending, marker);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new ProcessOutput(0, string.Empty);
        });
        var command = await context.RunCommand(["--runtime-parent", runtimeParent], [context.Executable], deadline.Token, runner);
        command.ExitCode.ShouldBe(cleanupBeforeStop ? 17 : 130, command.Error);
        if (cleanupBeforeStop)
        {
            File.Move(pending, marker);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ProcessTree.Terminate(_sibling, TimeSpan.FromSeconds(2));
        _sibling.Dispose();
        _directory.Dispose();
    }

    private Task WaitForReady(CancellationToken ct) => WaitForMarker(_ready, ct);

    private static async Task WaitForMarker(string marker, CancellationToken ct)
    {
        while (!File.Exists(marker))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
        }
    }

    private static ProcessStartInfo CreateWorker(string marker, string role, bool failCleanup, bool cleanupBeforeStop)
    {
        var dotnet = PathExecutableResolver.Default.Resolve("dotnet");
        var start = new ProcessStartInfo(dotnet) { UseShellExecute = false };
        start.ArgumentList.Add(typeof(ResourceBudgetPodmanCompositionFixture).Assembly.Location);
        start.ArgumentList.Add("--filter-method");
        start.ArgumentList.Add(typeof(ResourceBudgetPodmanCompositionTests).FullName + "."
            + nameof(ResourceBudgetPodmanCompositionTests.Runs_the_nested_workload_when_launched_by_the_outer_budget_fixture));
        start.ArgumentList.Add("--explicit");
        start.ArgumentList.Add("only");
        start.Environment[MarkerVariable] = marker;
        start.Environment[RoleVariable] = role;
        start.Environment[FailCleanupVariable] = failCleanup ? "1" : "0";
        start.Environment[CleanupBeforeStopVariable] = cleanupBeforeStop ? "1" : "0";
        start.Environment["TMPDIR"] = Path.GetDirectoryName(marker);
        return start;
    }
}
