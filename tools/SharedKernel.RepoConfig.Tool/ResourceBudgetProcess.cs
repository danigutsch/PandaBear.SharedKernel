using System.Diagnostics;
using System.Globalization;
using SharedKernel.Processes;
using static SharedKernel.RepoConfig.Tool.ResourceBudgetGuard;

namespace SharedKernel.RepoConfig.Tool;

/// <summary>Runs the literal command and observes its Linux process tree when stopped.</summary>
internal static class ResourceBudgetProcess
{
    private const int ObservationLimit = 4096;
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Returns the natural exit or stops and verifies the observed owned tree after cancellation.</summary>
    internal static async Task<ChildResult> Run(ProcessStartInfo startInfo, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return new ChildResult(null, Started: false);
        }

        Process process;
        try
        {
            process = ProcessTree.Start(startInfo);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return new ChildResult(null, Started: false);
        }

        using (process)
        {
            var root = ReadProcess(process.Id, out _);
            try
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
                return new ChildResult(process.ExitCode);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                var observed = Capture(root);
                await ProcessTree.Terminate(process, ExitTimeout).ConfigureAwait(false);
                return new ChildResult(null, await Verify(observed).ConfigureAwait(false));
            }
        }
    }

    private static Observation Capture(ProcessIdentity? root)
    {
        if (root is not { } expected || ReadProcess(expected.Id, out _) != expected)
        {
            return new Observation([], false);
        }

        List<ProcessIdentity> identities = [expected];
        HashSet<int> seen = [expected.Id];
        var threadCount = 0;
        var complete = true;
        for (var index = 0; index < identities.Count; index++)
        {
            var taskDirectory = ProcPath(identities[index].Id) + "/task";
            try
            {
                foreach (var thread in Directory.EnumerateDirectories(taskDirectory))
                {
                    if (++threadCount > ObservationLimit)
                    {
                        return new Observation(identities, false);
                    }

                    var children = ReadProc(thread + "/children", out var readable);
                    complete &= readable && (children is not null || !Directory.Exists(thread));
                    foreach (var child in children?.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries) ?? [])
                    {
                        if (!int.TryParse(child, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                        {
                            complete = false;
                            continue;
                        }

                        if (!seen.Add(id))
                        {
                            continue;
                        }

                        if (seen.Count > ObservationLimit)
                        {
                            return new Observation(identities, false);
                        }

                        var identity = ReadProcess(id, out var valid);
                        complete &= valid;
                        if (identity is { } running)
                        {
                            identities.Add(running);
                        }
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                // The process exited during observation.
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                complete = false;
            }
        }

        return new Observation(identities, complete);
    }

    private static async Task<Cleanup> Verify(Observation observed)
    {
        var complete = observed.Complete;
        var timer = Stopwatch.StartNew();
        int remaining;
        do
        {
            remaining = 0;
            foreach (var identity in observed.Identities)
            {
                if (ReadProcess(identity.Id, out var valid) == identity)
                {
                    remaining++;
                }

                complete &= valid;
            }

            if (remaining == 0 || timer.Elapsed >= ExitTimeout)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }
        while (true);

        return new Cleanup(complete, observed.Identities.Count, remaining);
    }

    private static ProcessIdentity? ReadProcess(int id, out bool readable)
    {
        var stat = ReadProc(ProcPath(id) + "/stat", out readable);
        if (stat is null)
        {
            return null;
        }

        var closingParenthesis = stat.LastIndexOf(')', StringComparison.Ordinal);
        var fields = stat[(closingParenthesis + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        ulong startTicks = 0;
        readable = closingParenthesis >= 0 && fields.Length > 19
            && ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out startTicks);
        if (!readable || fields[0] is "Z" or "X")
        {
            return null;
        }

        return new ProcessIdentity(id, startTicks);
    }

    private static string? ReadProc(string file, out bool readable)
    {
        readable = true;
        try
        {
            return File.ReadAllText(file);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            readable = false;
            return null;
        }
    }

    private static string ProcPath(int id) => "/proc/" + id.ToString(CultureInfo.InvariantCulture);

    private sealed record ProcessIdentity(int Id, ulong StartTicks);
    private sealed record Observation(IReadOnlyList<ProcessIdentity> Identities, bool Complete);
}
