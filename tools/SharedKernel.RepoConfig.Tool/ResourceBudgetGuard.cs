namespace SharedKernel.RepoConfig.Tool;

/// <summary>Applies caller limits before and during one owned command.</summary>
internal static class ResourceBudgetGuard
{
    /// <summary>Independent caller floors and optional Linux pressure policy.</summary>
    /// <param name="MinimumHostMemoryBytes">Minimum host MemAvailable.</param>
    /// <param name="MinimumScratchBytes">Minimum free bytes in the selected filesystem.</param>
    /// <param name="MinimumCgroupHeadroomBytes">Optional remaining headroom required under a visible finite memory cap.</param>
    internal sealed record Limits(long MinimumHostMemoryBytes, long MinimumScratchBytes, long? MinimumCgroupHeadroomBytes = null)
    {
        /// <summary>Gets the optional maximum Linux memory pressure average.</summary>
        internal decimal? MaximumMemoryPressurePercent { get; init; }
        /// <summary>Gets the number of consecutive samples above the maximum needed to stop.</summary>
        internal int ConsecutivePressureSamples { get; init; } = 2;
        /// <summary>Gets whether tmpfs and ramfs are forbidden for scratch.</summary>
        internal bool RejectMemoryBackedScratch { get; init; }
    }

    /// <summary>Measurements captured at one sample.</summary>
    /// <param name="HostAvailableBytes">Host MemAvailable, when measured.</param>
    /// <param name="CgroupMeasured">Whether the visible cgroup hierarchy was measured successfully.</param>
    /// <param name="CgroupHeadroomBytes">Remaining bytes under the tightest finite visible cap, if any.</param>
    /// <param name="ScratchAvailableBytes">Available bytes in the selected scratch filesystem.</param>
    /// <param name="ScratchFilesystem">The filesystem type, when available.</param>
    /// <param name="ShmemBytes">Host shared memory, when measured.</param>
    /// <param name="MemoryPressurePercent">Host memory PSI some avg10, when measured.</param>
    internal sealed record Sample(
        long? HostAvailableBytes, bool CgroupMeasured, long? CgroupHeadroomBytes,
        long? ScratchAvailableBytes, string? ScratchFilesystem, long? ShmemBytes, decimal? MemoryPressurePercent);

    /// <summary>Result of checking the observed process identities after shutdown.</summary>
    /// <param name="ObservationComplete">Whether the process walk and checks encountered no observation failures.</param>
    /// <param name="ObservedProcesses">Number of captured identities, including the direct child.</param>
    /// <param name="RemainingObservedProcesses">Number of observed identities that were still alive at the last check.</param>
    internal sealed record Cleanup(bool ObservationComplete, int ObservedProcesses, int RemainingObservedProcesses)
    {
        /// <summary>Gets the separately observed outcome of the opted-in private Podman cleanup.</summary>
        internal ResourceBudgetPodmanAbort.Outcome? PodmanTeardown { get; init; }
    }

    /// <summary>Natural child result or observed cancellation cleanup.</summary>
    /// <param name="ExitCode">The child's natural exit status, when observed.</param>
    /// <param name="Cleanup">Cleanup evidence when cancellation stopped the child.</param>
    /// <param name="Started">Whether the command started.</param>
    internal sealed record ChildResult(int? ExitCode, Cleanup? Cleanup = null, bool Started = true);

    /// <summary>Bounded outcome evidence for one invocation.</summary>
    /// <param name="Reason">Why the guard stopped, or None for a natural child exit.</param>
    /// <param name="Started">Whether the child started.</param>
    /// <param name="ExitCode">The child's natural exit status, when observed.</param>
    /// <param name="InitialSample">The preflight measurement.</param>
    /// <param name="LastSample">The most recent measurement.</param>
    /// <param name="SampleCount">The number of measurements taken.</param>
    /// <param name="Cleanup">Cleanup evidence when cancellation stopped the child.</param>
    internal sealed record Report(
        StopReason Reason, bool Started, int? ExitCode, Sample InitialSample, Sample LastSample, long SampleCount, Cleanup? Cleanup);

    /// <summary>Why the command was rejected or stopped.</summary>
    internal enum StopReason
    {
        /// <summary>The child exited naturally.</summary>
        None,
        /// <summary>A required measurement could not be obtained.</summary>
        MeasurementUnavailable,
        /// <summary>Host MemAvailable was below the caller floor.</summary>
        HostMemoryFloor,
        /// <summary>Finite cgroup headroom was below the caller floor.</summary>
        CgroupMemoryFloor,
        /// <summary>Scratch free space was below the caller floor.</summary>
        ScratchSpaceFloor,
        /// <summary>The scratch filesystem was disallowed by the caller.</summary>
        ScratchFilesystem,
        /// <summary>Pressure exceeded the configured limit in consecutive samples.</summary>
        MemoryPressure,
        /// <summary>The caller cancelled the command.</summary>
        Cancelled,
        /// <summary>The child executable did not start.</summary>
        LaunchFailed,
    }

    /// <summary>Samples resources, awaits the command or the next interval, and joins the child after any stop.</summary>
    internal static async Task<Report> Run(
        Limits limits, Func<Sample> measure, Func<CancellationToken, Task> waitForSample,
        Func<CancellationToken, Task<ChildResult>> start, CancellationToken ct)
    {
        var initial = measure();
        var reason = ct.IsCancellationRequested ? StopReason.Cancelled : Check(initial, limits);
        var pressureSamples = AbovePressureLimit(initial, limits) ? 1 : 0;
        if (reason == StopReason.None && pressureSamples >= limits.ConsecutivePressureSamples)
        {
            reason = StopReason.MemoryPressure;
        }

        if (reason != StopReason.None)
        {
            return new Report(reason, false, null, initial, initial, 1, null);
        }

        using var owned = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = start(owned.Token);
        var latest = initial;
        long sampleCount = 1;
        ChildResult child;
        try
        {
            while (!running.IsCompleted)
            {
                var interval = waitForSample(owned.Token);
                await Task.WhenAny(running, interval).ConfigureAwait(false);
                if (running.IsCompleted)
                {
                    break;
                }

                await interval.ConfigureAwait(false);
                if (ct.IsCancellationRequested)
                {
                    reason = StopReason.Cancelled;
                    break;
                }

                latest = measure();
                sampleCount++;
                reason = Check(latest, limits);
                if (reason != StopReason.None)
                {
                    break;
                }

                pressureSamples = AbovePressureLimit(latest, limits) ? pressureSamples + 1 : 0;
                if (pressureSamples >= limits.ConsecutivePressureSamples)
                {
                    reason = StopReason.MemoryPressure;
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            reason = StopReason.Cancelled;
        }
        finally
        {
            // Cancel the interval on natural exit as well, and let command cleanup finish before reporting.
            await owned.CancelAsync().ConfigureAwait(false);
            child = await running.ConfigureAwait(false);
        }

        if (reason == StopReason.None)
        {
            reason = child.ExitCode.HasValue ? StopReason.None
                : ct.IsCancellationRequested ? StopReason.Cancelled : StopReason.LaunchFailed;
        }

        return new Report(reason, child.Started, child.ExitCode, initial, latest, sampleCount, child.Cleanup);
    }

    private static StopReason Check(Sample sample, Limits limits)
    {
        if (sample.HostAvailableBytes is null || sample.ScratchAvailableBytes is null
            || (limits.MinimumCgroupHeadroomBytes.HasValue && (!sample.CgroupMeasured || sample.CgroupHeadroomBytes is null))
            || (limits.MaximumMemoryPressurePercent.HasValue && sample.MemoryPressurePercent is null)
            || (limits.RejectMemoryBackedScratch && sample.ScratchFilesystem is null))
        {
            return StopReason.MeasurementUnavailable;
        }

        if (sample.HostAvailableBytes < limits.MinimumHostMemoryBytes)
        {
            return StopReason.HostMemoryFloor;
        }

        if (limits.MinimumCgroupHeadroomBytes is { } minimumCgroupHeadroom && sample.CgroupHeadroomBytes < minimumCgroupHeadroom)
        {
            return StopReason.CgroupMemoryFloor;
        }

        if (sample.ScratchAvailableBytes < limits.MinimumScratchBytes)
        {
            return StopReason.ScratchSpaceFloor;
        }

        return limits.RejectMemoryBackedScratch && (sample.ScratchFilesystem is "tmpfs" or "ramfs")
            ? StopReason.ScratchFilesystem : StopReason.None;
    }

    private static bool AbovePressureLimit(Sample sample, Limits limits) =>
        limits.MaximumMemoryPressurePercent is { } maximum && sample.MemoryPressurePercent > maximum;
}
