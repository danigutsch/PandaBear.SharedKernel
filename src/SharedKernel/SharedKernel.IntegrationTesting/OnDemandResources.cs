using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace PandaBear.IntegrationTesting;

/// <summary>
/// A set of <see cref="OnDemandResource{T}"/> keyed by name: each key starts once, on its first demand, and every caller of that key shares
/// the outcome. Disposal disposes every started value.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="start">Starts the value of a key; it follows the rules of <see cref="OnDemandResource{T}"/>'s start delegate.</param>
public sealed class OnDemandResources<T>(Func<string, CancellationToken, Task<T>> start) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, OnDemandResource<T>> _resources = new(StringComparer.Ordinal);
    private readonly Func<string, CancellationToken, Task<T>> _start = start ?? throw new ArgumentNullException(nameof(start));
    private volatile bool _disposed;

    /// <summary>Gets the value of a key, starting it on the first demand.</summary>
    /// <param name="key">The key, such as a resource name.</param>
    /// <param name="ct">The caller's token; cancelling it abandons only this wait.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="ObjectDisposedException">The set was disposed.</exception>
    public Task<T> Get(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _resources.GetOrAdd(key, name => new OnDemandResource<T>(token => _start(name, token))).Get(ct);
    }

    /// <summary>Disposes every started value; failures are reported together after all were attempted.</summary>
    /// <returns>A task that completes when every value was disposed.</returns>
    /// <exception cref="AggregateException">One or more values failed to dispose.</exception>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Every value is disposed before the failures are reported.")]
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        var failures = new List<Exception>();
        foreach (var resource in _resources.Values)
        {
            try
            {
                await resource.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Disposing the on-demand resources failed.", failures);
        }
    }
}
