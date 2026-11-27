using System.Diagnostics;
using System.Runtime.Versioning;

namespace SharedKernel.RepoConfig.Tool;

/// <summary>Coordinates a bounded abort with isolated Podman commands in a trusted child process tree.</summary>
internal sealed class ResourceBudgetPodmanAbort : IDisposable
{
    /// <summary>Names the outer command option that enables cooperative teardown.</summary>
    internal const string OptionName = "--cooperative-podman-cleanup";

    /// <summary>Allows the outer CLI to finish cooperative teardown on caller cancellation.</summary>
    internal static readonly TimeSpan ProcessTerminationTimeout = TimeSpan.FromMinutes(3);

    private const string EnvironmentVariable = "SHAREDKERNEL_REPO_BUDGET_ABORT";
    private const string ControlPrefix = "sharedkernel-budget-abort-";
    private const string ParticipantPrefix = "podman-";
    private const string StopName = "stop";
    private const string AcknowledgedSuffix = ".acknowledged";
    private const string CompleteSuffix = ".complete";
    private const string RetainedSuffix = ".retained";
    private const string UnavailableSuffix = ".unavailable";
    private const int CancelledExitCode = 130;
    private const UnixFileMode OwnerAccess = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan AcknowledgementPeriod = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(140);
    private static readonly TimeSpan RelayExitGrace = TimeSpan.FromSeconds(8);
    private readonly string _directory;
    private bool _retainControl;

    private ResourceBudgetPodmanAbort(string directory) => _directory = directory;

    /// <summary>Describes the nested engine's status separately from process-tree observation.</summary>
    internal enum Outcome
    {
        /// <summary>No active isolated engine enrolled in cleanup.</summary>
        NoActiveEngine,
        /// <summary>Each enrolled engine completed verified cleanup or stopped before allocation.</summary>
        Completed,
        /// <summary>An enrolled engine reported that its private resources remained.</summary>
        Retained,
        /// <summary>An enrolled engine did not acknowledge cancellation before the short deadline.</summary>
        Unacknowledged,
        /// <summary>An enrolled engine acknowledged cancellation but did not report a final status.</summary>
        TimedOut,
        /// <summary>The guard could not establish the cleanup outcome.</summary>
        Unavailable,
    }

    /// <summary>Passes a fresh owner-only rendezvous to the explicitly opted-in child.</summary>
    /// <exception cref="InvalidOperationException">The selected Linux scratch directory is unavailable.</exception>
    internal static ResourceBudgetPodmanAbort Create(ProcessStartInfo child)
    {
        if (!OperatingSystem.IsLinux() || child.Environment["TMPDIR"] is not { } scratch
            || !Path.IsPathFullyQualified(scratch) || !Directory.Exists(scratch))
        {
            throw new InvalidOperationException("Cooperative Podman cleanup requires an existing Linux scratch directory.");
        }

        var directory = CreatePrivateDirectory(scratch, ControlPrefix);
        child.Environment[EnvironmentVariable] = directory;
        return new ResourceBudgetPodmanAbort(directory);
    }

    /// <summary>Requests owned engine teardown before the existing forceful process-tree fallback.</summary>
    internal async Task<Outcome> RequestStop(Process child)
    {
        try
        {
            VerifyDirectory(_directory);
            CreateMarker(Path.Combine(_directory, StopName));
            var participants = Participants();
            if (participants.Length == 0)
            {
                return Outcome.NoActiveEngine;
            }

            if (!await WaitFor(participants, Acknowledged, AcknowledgementPeriod).ConfigureAwait(false))
            {
                _retainControl = true;
                return Outcome.Unacknowledged;
            }

            if (!await WaitFor(participants, HasFinished, GracePeriod).ConfigureAwait(false))
            {
                _retainControl = true;
                return Outcome.TimedOut;
            }

            var outcome = ReadOutcome(participants);
            _retainControl = outcome is Outcome.Retained or Outcome.Unavailable;
            await AllowRelayExit(child).ConfigureAwait(false);
            return outcome;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _retainControl = true;
            return Outcome.Unavailable;
        }
    }

    /// <summary>Lets the engine own cancellation and acknowledge its actual teardown outcome after a guard stop.</summary>
    /// <exception cref="InvalidOperationException">The inherited cooperative cleanup path is invalid.</exception>
    internal static async Task<int> RunParticipant(
        Func<CancellationToken, Action<bool>?, Task<int>> run, CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) is not { } directory)
        {
            return await run(ct, null).ConfigureAwait(false);
        }

        if (!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(directory)
            || !Path.GetFileName(directory).StartsWith(ControlPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Podman budget cleanup directory is invalid.");
        }

        VerifyDirectory(directory);
        if (MarkerExists(directory, StopName))
        {
            return CancelledExitCode;
        }

        var participant = Path.Combine(directory, ParticipantPrefix + Guid.NewGuid().ToString("N"));
        CreateMarker(participant);
        bool? cleaned = null;
        try
        {
            // Publication precedes the second stop check, so a racing guard either waits or the engine never starts.
            if (MarkerExists(directory, StopName))
            {
                cleaned = true;
                return CancelledExitCode;
            }

            using var engine = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var observation = new CancellationTokenSource();
            var observeStop = ObserveStop(directory, participant, engine.CancelAsync, observation.Token);
            try
            {
                return await run(engine.Token, result => cleaned = result).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (engine.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                return CancelledExitCode;
            }
            finally
            {
                await observation.CancelAsync().ConfigureAwait(false);
                await observeStop.ConfigureAwait(false);
            }
        }
        finally
        {
            VerifyDirectory(directory);
            var suffix = cleaned switch
            {
                true => CompleteSuffix,
                false => RetainedSuffix,
                null => UnavailableSuffix,
            };
            File.Move(participant, participant + suffix);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_retainControl)
        {
            return;
        }

        try
        {
            VerifyDirectory(_directory);
            if (Participants().Any(HasAdverseOutcome))
            {
                return;
            }

            foreach (var marker in Directory.EnumerateFiles(_directory, ParticipantPrefix + "*"))
            {
                var name = Path.GetFileNameWithoutExtension(marker);
                if (Guid.TryParseExact(name.AsSpan(ParticipantPrefix.Length), "N", out _))
                {
                    File.Delete(marker);
                }
            }

            File.Delete(Path.Combine(_directory, StopName));
            Directory.Delete(_directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep an unverified or nonempty directory rather than removing anything outside the handshake.
        }
    }

    private string[] Participants() =>
        [.. Directory.EnumerateFiles(_directory, ParticipantPrefix + "*")
            .Where(path => Path.GetExtension(path) is "" or RetainedSuffix or UnavailableSuffix)
            .Select(path => Path.Combine(_directory, Path.GetFileNameWithoutExtension(path)))
            .Where(path => Guid.TryParseExact(Path.GetFileName(path).AsSpan(ParticipantPrefix.Length), "N", out _))
            .Distinct(StringComparer.Ordinal)];

    private static bool HasFinished(string participant) =>
        !File.Exists(participant) || File.Exists(participant + CompleteSuffix) || File.Exists(participant + RetainedSuffix)
        || File.Exists(participant + UnavailableSuffix);

    private static bool Acknowledged(string participant) => HasFinished(participant) || File.Exists(participant + AcknowledgedSuffix);

    private static bool HasAdverseOutcome(string participant) =>
        File.Exists(participant + RetainedSuffix) || File.Exists(participant + UnavailableSuffix);

    private static Outcome ReadOutcome(string[] participants)
    {
        if (participants.Any(participant => File.Exists(participant + RetainedSuffix)))
        {
            return Outcome.Retained;
        }

        return participants.All(participant => File.Exists(participant + CompleteSuffix)) ? Outcome.Completed : Outcome.Unavailable;
    }

    private static bool MarkerExists(string directory, string marker) => File.Exists(Path.Combine(directory, marker));

    private static void CreateMarker(string path)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
    }

    [SupportedOSPlatform("linux")]
    private static string CreatePrivateDirectory(string parent, string prefix) =>
        Directory.CreateDirectory(Path.Combine(parent, prefix + Guid.NewGuid().ToString("N")), OwnerAccess).FullName;

    private static void VerifyDirectory(string path)
    {
        if (!OperatingSystem.IsLinux() || File.GetAttributes(path) != FileAttributes.Directory
            || File.GetUnixFileMode(path) != OwnerAccess)
        {
            throw new IOException("The Podman budget cleanup directory is missing or its ownership boundary changed.");
        }
    }

    private static async Task ObserveStop(
        string directory, string participant, Func<Task> cancelEngine, CancellationToken ct)
    {
        try
        {
            while (!MarkerExists(directory, StopName))
            {
                await Task.Delay(Interval, ct).ConfigureAwait(false);
            }

            CreateMarker(participant + AcknowledgedSuffix);
            await cancelEngine().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The engine completed before the guard requested a stop.
        }
    }

    private static async Task<bool> WaitFor(string[] participants, Func<string, bool> ready, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            while (!participants.All(ready))
            {
                await Task.Delay(Interval, deadline.Token).ConfigureAwait(false);
            }

            return true;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task AllowRelayExit(Process child)
    {
        using var deadline = new CancellationTokenSource(RelayExitGrace);
        try
        {
            await child.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            // The owning guard applies the ordinary process-tree fallback if its command outlives the engine.
        }
    }
}
