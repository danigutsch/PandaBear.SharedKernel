using System.Diagnostics;
using SharedKernel.Processes;

namespace SharedKernel.DevContainers.Tests;

/// <summary>Supplies cgroup files and private Podman observations without launching processes.</summary>
internal sealed class IsolatedPodmanCgroupFixture : IProcessRunner
{
    /// <summary>The selected user slice unit.</summary>
    internal const string Slice = "budget.slice";

    /// <summary>The physical cgroup reported for the selected slice.</summary>
    internal const string Parent = "/fixture/budget.slice";

    /// <summary>The expected launcher membership under the slice.</summary>
    internal const string Launcher = Parent + "/launcher.scope";
    private const string Payload = Parent + "/libpod.scope/container";
    private const string Proc = "/fixture/proc";
    private const string Container = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private readonly TaskCompletionSource<ProcessOutput> _child = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task<ProcessOutput>? _runningChild;

    /// <summary>The resolved executable supplied to the synthetic systemd manager.</summary>
    internal string? SystemctlExecutable { get; set; } = Path.Combine(Path.GetTempPath(), "systemctl-fixture");

    /// <summary>Overrides the user manager response for slice resolution failure tests.</summary>
    internal PodmanCommandResult? ManagerResult { get; set; }

    /// <summary>An optional failure when reading the launcher's membership.</summary>
    internal Exception? LauncherReadFailure { get; set; }

    /// <summary>The launcher membership reported by the fixture.</summary>
    internal string LauncherCgroup { get; set; } = Launcher;

    /// <summary>The payload membership reported by the fixture.</summary>
    internal string PayloadCgroup { get; set; } = Payload;

    /// <summary>The slice's original memory limit.</summary>
    internal string? MaximumMemory { get; set; } = "16384";

    /// <summary>The slice's swap limit.</summary>
    internal string? MaximumSwap { get; set; } = "0";

    /// <summary>An optional memory limit reported after the child launches.</summary>
    internal string? MemoryAfterLaunch { get; set; }

    /// <summary>An optional slice memory limit reported as soon as the child starts, before the first inspection.</summary>
    internal string? MemoryAtChildStart { get; set; }

    /// <summary>The effective leaf controls reported independently for both process scopes.</summary>
    internal (string Memory, string Swap) MemberLimits { get; set; } = ("max", "max");

    /// <summary>Whether the child returns during the second inspection.</summary>
    internal bool CompleteChildOnConfirmation { get; set; }

    /// <summary>Whether the second inspection sees a new start time with the same process IDs.</summary>
    internal bool RestartOnConfirmation { get; set; }

    /// <summary>An optional unexpected inventory failure after the owned child starts.</summary>
    internal FormatException? InventoryFailure { get; set; }

    /// <summary>Supplies exceptional private responses, with the current container inspection count.</summary>
    internal Func<string[], int, PodmanCommandResult?>? QueryOverride { get; set; }

    /// <summary>Completes the child synchronously with the supplied status.</summary>
    internal int? ImmediateExitCode { get; set; }

    /// <summary>The number of container inspections made through the private engine.</summary>
    internal int InspectionCount { get; private set; }

    /// <summary>The number of private configuration checks before observations.</summary>
    internal int VerifiedFiles { get; private set; }

    /// <summary>The number of user systemd manager queries.</summary>
    internal int ManagerQueries { get; private set; }

    /// <summary>The token passed to the owned child.</summary>
    internal CancellationToken ChildToken { get; private set; }

    /// <summary>Whether the task returned to the wrapper has finished.</summary>
    internal bool ChildCompleted => _runningChild?.IsCompleted == true;

    /// <summary>The fixture command observed by the process runner.</summary>
    internal ProcessStartInfo Command { get; } = new(Path.Combine(Path.GetTempPath(), "isolated-child-fixture"));

    /// <summary>Resolves the slice through the synthetic systemd and cgroup readers.</summary>
    internal Task<IsolatedPodmanCgroupBudget> Resolve(CancellationToken ct, string slice = Slice) =>
        IsolatedPodmanCgroupBudget.Resolve(new PodmanDnsHost(null, null, Proc, true, Systemd), slice, SystemctlExecutable, Read, ct);

    /// <summary>Records each configuration check requested by the wrapper.</summary>
    internal void VerifyFiles() => VerifiedFiles++;

    /// <summary>Answers one private engine inspection.</summary>
    /// <exception cref="InvalidOperationException">The fixture received an unexpected query.</exception>
    /// <exception cref="FormatException">The fixture injected an unexpected inventory failure.</exception>
    internal Task<PodmanCommandResult> Query(string[] arguments, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (arguments[0] == "container")
        {
            InspectionCount++;
        }
        if (QueryOverride?.Invoke(arguments, InspectionCount) is { } result)
        {
            return Task.FromResult(result);
        }

        var response = arguments[0] switch
        {
            "info" => "systemd\n",
            "ps" when ChildToken.CanBeCanceled && InventoryFailure is { } failure => throw failure,
            "ps" => Container + "\n",
            "container" => Inspect(arguments[3]),
            _ => throw new InvalidOperationException("Unexpected private Podman query."),
        };
        return Task.FromResult(new PodmanCommandResult(0, response, string.Empty));
    }

    /// <inheritdoc />
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        ChildToken = cancellationToken;
        _runningChild = ImmediateExitCode is { } exitCode
            ? Task.FromResult(new ProcessOutput(exitCode, "preserved output")) : _child.Task.WaitAsync(cancellationToken);
        return _runningChild;
    }

    /// <summary>Releases the fixture's child even when the wrapper did not cancel it.</summary>
    internal Task ReleaseChild()
    {
        _child.TrySetResult(new ProcessOutput(0, "fixture released"));
        return _runningChild is { } running ? running : Task.CompletedTask;
    }

    private Task<PodmanCommandResult> Systemd(ProcessStartInfo startInfo, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        startInfo.FileName.ShouldBe(SystemctlExecutable);
        startInfo.ArgumentList.ShouldBe(["--user", "show", "--property=Id,LoadState,ActiveState,ControlGroup", "--", Slice]);
        ManagerQueries++;
        return Task.FromResult(ManagerResult ?? new PodmanCommandResult(0,
            $"Id={Slice}\nLoadState=loaded\nActiveState=active\nControlGroup={Parent}\n", string.Empty));
    }

    private string Inspect(string format)
    {
        if (InspectionCount == 2 && CompleteChildOnConfirmation)
        {
            _child.SetResult(new ProcessOutput(0, "preserved output"));
        }

        var started = InspectionCount > 1 && RestartOnConfirmation ? "2026-11-26T12:00:01Z" : "2026-11-26T12:00:00Z";
        return string.Join('\t', format.Split('\t').Select(field => field switch
        {
            "{{.ID}}" => Container,
            "{{.State.Running}}" => "true",
            "{{.State.Paused}}" => "false",
            "{{.State.Pid}}" => "1001",
            "{{.State.ConmonPid}}" => "1002",
            "{{.State.StartedAt}}" => started,
            _ => throw new InvalidOperationException("Unexpected private Podman inspection field."),
        })) + "\n";
    }

    private string? Read(string file)
    {
        file = file.Replace('\\', '/');
        return file switch
        {
            Proc + "/self/mountinfo" => "35 29 0:28 / /sys/fs/cgroup rw,nosuid,nodev,noexec,relatime - cgroup2 cgroup rw\n",
            Proc + "/self/cgroup" when LauncherReadFailure is { } failure => throw failure,
            Proc + "/self/cgroup" => "0::" + LauncherCgroup,
            Proc + "/1001/stat" => "1001 (payload) S " + string.Concat(Enumerable.Repeat("0 ", 18)) + "55",
            Proc + "/1001/cgroup" => "0::" + PayloadCgroup,
            Proc + "/1002/stat" => "1002 (conmon) S " + string.Concat(Enumerable.Repeat("0 ", 18)) + "56",
            Proc + "/1002/cgroup" => "0::" + Launcher,
            "/sys/fs/cgroup" + Parent + "/memory.max" =>
                (ChildToken.CanBeCanceled ? MemoryAtChildStart : null)
                ?? (InspectionCount > 0 ? MemoryAfterLaunch : null)
                ?? MaximumMemory,
            "/sys/fs/cgroup" + Parent + "/memory.swap.max" => MaximumSwap,
            "/sys/fs/cgroup" + Payload + "/memory.max" or "/sys/fs/cgroup" + Launcher + "/memory.max" => MemberLimits.Memory,
            "/sys/fs/cgroup" + Payload + "/memory.swap.max" or "/sys/fs/cgroup" + Launcher + "/memory.swap.max" => MemberLimits.Swap,
            _ when file.StartsWith("/sys/fs/cgroup/", StringComparison.Ordinal) => "max",
            _ => null,
        };
    }
}
