using System.Diagnostics;
using System.Globalization;
using SharedKernel.Processes;

namespace SharedKernel.DevContainers;

/// <summary>Verifies observed private Podman members against the budget of a caller-owned systemd user slice.</summary>
internal sealed class IsolatedPodmanCgroupBudget
{
    private const string BoundaryFailure = "The caller-owned user systemd slice and its finite memory and swap limits could not be verified.";
    private const string ObservationFailure = "Could not verify live container placement for the private Podman engine.";
    private const string MemberFormat = "{{.ID}}\t{{.State.Running}}\t{{.State.Paused}}\t{{.State.Pid}}\t{{.State.ConmonPid}}\t{{.State.StartedAt}}";
    private static readonly TimeSpan ObservationInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private readonly string _parent;
    private readonly string _procRoot;
    private readonly long _maximumMemory;
    private readonly long _maximumSwap;
    private readonly Func<string, string?> _read;

    private IsolatedPodmanCgroupBudget(
        string parent, string procRoot, long maximumMemory, long maximumSwap, Func<string, string?> read)
    {
        _parent = parent;
        _procRoot = procRoot;
        _maximumMemory = maximumMemory;
        _maximumSwap = maximumSwap;
        _read = read;
    }

    /// <summary>Resolves the exact active user slice and verifies its physical ancestry and finite budget before engine setup.</summary>
    internal static Task<IsolatedPodmanCgroupBudget> Resolve(PodmanDnsHost host, string slice, CancellationToken ct)
    {
        var systemctl = new PathExecutableResolver(string.Concat("/usr/bin", Path.PathSeparator, "/bin"), isWindows: false)
            .Find("systemctl");
        return Resolve(host, slice, systemctl, ReadFile, ct);
    }

    /// <summary>Resolves the same boundary with a supplied filesystem reader and systemd executable.</summary>
    /// <exception cref="ArgumentException">The selected unit name is invalid.</exception>
    /// <exception cref="InvalidOperationException">The selected user slice or its finite cgroup budget could not be verified.</exception>
    internal static async Task<IsolatedPodmanCgroupBudget> Resolve(
        PodmanDnsHost host, string slice, string? systemctl, Func<string, string?> read, CancellationToken ct)
    {
        if (slice.Length is <= 6 or > 255 || slice == "-.slice" || !slice.EndsWith(".slice", StringComparison.Ordinal)
            || slice.Any(static character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.' or ':')))
        {
            throw new ArgumentException("The cgroup budget parent must name a user systemd slice.", nameof(slice));
        }

        if (systemctl is null || !Path.IsPathFullyQualified(systemctl))
        {
            throw new InvalidOperationException(BoundaryFailure);
        }

        var startInfo = new ProcessStartInfo(systemctl)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "--user", "show", "--property=Id,LoadState,ActiveState,ControlGroup", "--", slice })
        {
            startInfo.ArgumentList.Add(argument);
        }
        ProcessEnvironment.RemoveForgeTokens(startInfo);
        var resolved = await host.RunProcess(startInfo, ct).ConfigureAwait(false);
        if (resolved.ExitCode != 0 || resolved.StandardOutput.Length > 4096)
        {
            throw new InvalidOperationException(BoundaryFailure);
        }

        var values = SplitLines(resolved.StandardOutput);
        const string groupPrefix = "ControlGroup=";
        var group = values.Where(static line => line.StartsWith(groupPrefix, StringComparison.Ordinal)).ToArray();
        if (!values.Contains("Id=" + slice, StringComparer.Ordinal) || !values.Contains("LoadState=loaded", StringComparer.Ordinal)
            || !values.Contains("ActiveState=active", StringComparer.Ordinal) || group is not [var declaredGroup]
            || !declaredGroup.EndsWith("/" + slice, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(BoundaryFailure);
        }

        var parent = declaredGroup[groupPrefix.Length..];
        try
        {
            var self = Path.Combine(host.ProcRoot, "self");
            var launcher = LinuxCgroupPlacement.ReadUnifiedCgroup(read(Path.Combine(self, "cgroup")));
            if (launcher is not null && LinuxCgroupPlacement.HasUnifiedRootMount(read(Path.Combine(self, "mountinfo")))
                && LinuxCgroupPlacement.ReadParentLimits(parent, launcher, read) is { } limits)
            {
                return new IsolatedPodmanCgroupBudget(parent, host.ProcRoot, limits.MemoryMaximumBytes, limits.SwapMaximumBytes, read);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(BoundaryFailure, exception);
        }

        throw new InvalidOperationException(BoundaryFailure);
    }

    /// <summary>Runs the child while observing private containers and joins the observer before engine teardown.</summary>
    /// <exception cref="InvalidOperationException">The private engine or an observed member could not be verified.</exception>
    internal async Task<ProcessOutput> Run(
        ProcessStartInfo command, IProcessRunner runner, Action verifyFiles,
        Func<string[], CancellationToken, Task<PodmanCommandResult>> query, TextWriter error, CancellationToken ct)
    {
        var info = await query(["info", "--format", "{{.Host.CgroupManager}}"], ct).ConfigureAwait(false);
        if (info.ExitCode != 0 || !string.Equals(info.StandardOutput.TrimEnd('\r', '\n'), "systemd", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The isolated cgroup budget requires local rootless Podman with the systemd cgroup manager.");
        }

        using var childLifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var observationLifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var child = runner.Run(command, forward: null, childLifetime.Token);
        var observation = Observe(verifyFiles, query, observationLifetime.Token);
        try
        {
            if (await Task.WhenAny(child, observation).ConfigureAwait(false) == observation
                && (await observation.ConfigureAwait(false)).Failure is { } placementFailure && !child.IsCompleted)
            {
                await childLifetime.CancelAsync().ConfigureAwait(false);
                try
                {
                    _ = await child.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // The placement failure canceled this owned child.
                }

                throw new InvalidOperationException(placementFailure);
            }

            var result = await child.ConfigureAwait(false);
            await observationLifetime.CancelAsync().ConfigureAwait(false);
            var (observed, failure) = await observation.ConfigureAwait(false);
            if (result.ExitCode == 0)
            {
                if (failure is not null)
                {
                    throw new InvalidOperationException(failure);
                }
                if (!observed)
                {
                    throw new InvalidOperationException("No complete live private Podman payload and conmon pair was observed in the requested cgroup budget.");
                }
            }
            else if (failure is not null)
            {
                await error.WriteLineAsync(failure.AsMemory(), ct).ConfigureAwait(false);
            }
            return result;
        }
        finally
        {
            await observationLifetime.CancelAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (!child.IsCompleted)
            {
                await childLifetime.CancelAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            await Task.WhenAll(child, observation).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task<(bool Observed, string? Failure)> Observe(
        Action verifyFiles, Func<string[], CancellationToken, Task<PodmanCommandResult>> query, CancellationToken ct)
    {
        var observed = false;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var probe = CancellationTokenSource.CreateLinkedTokenSource(ct);
                probe.CancelAfter(ProbeTimeout);
                if (!HasParentBudget())
                {
                    return (observed, BoundaryFailure);
                }
                verifyFiles();
                var ids = await RunningContainers(query, probe.Token).ConfigureAwait(false);
                foreach (var id in ids)
                {
                    var members = await Inspect(id, query, probe.Token).ConfigureAwait(false);
                    if (members is not { } live)
                    {
                        continue;
                    }

                    var failure = VerifyMember(live.Payload, "payload") ?? VerifyMember(live.Conmon, "conmon");
                    if (await Inspect(id, query, probe.Token).ConfigureAwait(false) != members)
                    {
                        continue;
                    }

                    if (failure is not null)
                    {
                        return (observed, failure);
                    }
                    if (!HasParentBudget())
                    {
                        return (observed, BoundaryFailure);
                    }

                    observed = true;
                }

                await Task.Delay(ObservationInterval, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The child or caller stopped the observation.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            return (observed, ObservationFailure);
        }

        return (observed, null);
    }

    private bool HasParentBudget()
    {
        var launcher = LinuxCgroupPlacement.ReadUnifiedCgroup(_read(Path.Combine(_procRoot, "self", "cgroup")));
        return launcher is not null && LinuxCgroupPlacement.ReadParentLimits(_parent, launcher, _read) is { } limits
            && MeetsCeiling(limits);
    }

    private string? VerifyMember(int pid, string role)
    {
        var member = LinuxCgroupPlacement.ReadStableProcessCgroup(_procRoot, pid, _read);
        if (member is null)
        {
            return $"The live private Podman {role} identity or cgroup could not be verified.";
        }
        if (!LinuxCgroupPlacement.IsStrictDescendant(member, _parent))
        {
            return $"A live private Podman {role} is outside the requested cgroup budget.";
        }
        if (LinuxCgroupPlacement.ReadEffectiveLimits(member, _read) is not { } limits)
        {
            return $"The effective memory and swap limits of a live private Podman {role} could not be verified.";
        }

        return MeetsCeiling(limits) ? null
            : FormattableString.Invariant($"A live private Podman {role} exceeds the requested cgroup limits. Effective memory/swap bytes: {limits.MemoryMaximumBytes}/{limits.SwapMaximumBytes}; requested ceiling: {_maximumMemory}/{_maximumSwap}.");
    }

    private bool MeetsCeiling((long MemoryMaximumBytes, long SwapMaximumBytes) limits) =>
        limits.MemoryMaximumBytes <= _maximumMemory && limits.SwapMaximumBytes <= _maximumSwap;

    private static async Task<string[]> RunningContainers(
        Func<string[], CancellationToken, Task<PodmanCommandResult>> query, CancellationToken ct)
    {
        var result = await query(["ps", "--quiet", "--no-trunc", "--filter", "status=running", "--filter", "status=paused"], ct)
            .ConfigureAwait(false);
        var ids = SplitLines(result.StandardOutput);
        return result.ExitCode == 0 && ids.All(static id => id.Length == 64 && id.All(char.IsAsciiHexDigitLower))
            ? ids : throw new InvalidOperationException(ObservationFailure);
    }

    private static async Task<(int Payload, int Conmon, string StartedAt)?> Inspect(
        string id, Func<string[], CancellationToken, Task<PodmanCommandResult>> query, CancellationToken ct)
    {
        var result = await query(["container", "inspect", "--format", MemberFormat, id], ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            // A container can be removed between the list and inspect calls.
            var remaining = await RunningContainers(query, ct).ConfigureAwait(false);
            return remaining.Contains(id, StringComparer.Ordinal) ? throw new InvalidOperationException(ObservationFailure) : null;
        }

        var fields = result.StandardOutput.TrimEnd('\r', '\n').Split('\t');
        if (fields is not [var actual, var running, var paused, var payload, var conmon, var startedAt]
            || !string.Equals(id, actual, StringComparison.Ordinal)
            || !bool.TryParse(running, out var isRunning) || !bool.TryParse(paused, out var isPaused)
            || string.IsNullOrWhiteSpace(startedAt))
        {
            throw new InvalidOperationException(ObservationFailure);
        }

        return isRunning || isPaused ? (ReadPid(payload), ReadPid(conmon), startedAt) : null;
    }

    private static int ReadPid(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid > 0 ? pid : 0;

    private static string[] SplitLines(string text) => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private static string? ReadFile(string file)
    {
        try
        {
            return File.ReadAllText(file);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
