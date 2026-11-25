using System.Collections.Concurrent;
using Aspire.Hosting.ApplicationModel;

namespace SharedKernel.IntegrationTesting;

/// <summary>Keeps the last lines each resource of an application logged, so a failed wait or test can say why.</summary>
internal sealed class ResourceLogTail : IAsyncDisposable
{
    private const int Kept = 50;

    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _lines = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _watchers = [];

    private ResourceLogTail()
    {
    }

    /// <summary>Starts watching the logs of every resource.</summary>
    /// <param name="logs">The application's resource logger service.</param>
    /// <param name="resources">The resources; the lines of all instances of a resource are kept under its name.</param>
    /// <returns>The tail; dispose it to stop watching.</returns>
    internal static ResourceLogTail Watch(ResourceLoggerService logs, IEnumerable<IResource> resources)
    {
        var tail = new ResourceLogTail();
        foreach (var resource in resources)
        {
            var lines = tail._lines.GetOrAdd(resource.Name, _ => new ConcurrentQueue<string>());
            tail._watchers.Add(Task.Run(() => Follow(logs, resource, lines, tail._stop.Token)));
        }

        return tail;
    }

    /// <summary>The last lines a resource logged.</summary>
    /// <param name="resourceName">The resource.</param>
    /// <returns>The lines, oldest first; empty for a resource that is not watched.</returns>
    internal IReadOnlyList<string> Lines(string resourceName) => _lines.TryGetValue(resourceName, out var lines) ? [.. lines] : [];

    /// <summary>Gets the latest published lines, including any the background watcher has yet to consume.</summary>
    /// <param name="logs">The application's resource logger service.</param>
    /// <param name="resource">The resource whose backlog to read.</param>
    /// <returns>At most 50 lines in publication order.</returns>
    internal static async Task<IReadOnlyList<string>> ReadAvailable(ResourceLoggerService logs, IResource resource)
    {
        var lines = new Queue<string>(Kept);
        await foreach (var batch in logs.GetAllAsync(resource).ConfigureAwait(false))
        {
            foreach (var line in batch)
            {
                if (lines.Count == Kept)
                {
                    _ = lines.Dequeue();
                }

                lines.Enqueue(FormatLine(line));
            }
        }

        return [.. lines];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_watchers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping the watch is the expected end.
        }

        _stop.Dispose();
    }

    private static async Task Follow(ResourceLoggerService logs, IResource resource, ConcurrentQueue<string> lines, CancellationToken ct)
    {
        // The resource overload follows every instance (the orchestrator names them with a suffix), which a plain name would miss.
        await foreach (var batch in logs.WatchAsync(resource).WithCancellation(ct).ConfigureAwait(false))
        {
            foreach (var line in batch)
            {
                lines.Enqueue(FormatLine(line));
                while (lines.Count > Kept)
                {
                    _ = lines.TryDequeue(out _);
                }
            }
        }
    }

    private static string FormatLine(LogLine line) => (line.IsErrorMessage ? "err: " : string.Empty) + line.Content;
}
