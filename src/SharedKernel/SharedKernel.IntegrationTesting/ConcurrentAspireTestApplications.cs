using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.IntegrationTesting;

/// <summary>
/// Owns two concurrently started Aspire test applications.
/// </summary>
public sealed class ConcurrentAspireTestApplications : IAsyncDisposable
{
    private ConcurrentAspireTestApplications(AspireTestApplication first, AspireTestApplication second)
    {
        First = first;
        Second = second;
    }

    /// <summary>
    /// Gets the first started application.
    /// </summary>
    public AspireTestApplication First { get; }

    /// <summary>
    /// Gets the second started application.
    /// </summary>
    public AspireTestApplication Second { get; }

    /// <summary>
    /// Starts two AppHost instances concurrently.
    /// </summary>
    /// <typeparam name="TAppHost">The AppHost entry-point type.</typeparam>
    /// <param name="healthyResourceNames">Resource names that must become healthy before returning.</param>
    /// <param name="createAppHostArguments">Creates isolated AppHost configuration arguments for each instance.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The concurrently started applications.</returns>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The returned applications are owned by this type and disposed after a failed start.")]
    public static async Task<ConcurrentAspireTestApplications> Start<TAppHost>(
        IEnumerable<string> healthyResourceNames,
        Func<IReadOnlyCollection<string>> createAppHostArguments,
        CancellationToken ct)
        where TAppHost : class
    {
        ArgumentNullException.ThrowIfNull(healthyResourceNames);
        ArgumentNullException.ThrowIfNull(createAppHostArguments);

        var firstStart = AspireTestApplication.Start<TAppHost>(
            healthyResourceNames,
            null,
            createAppHostArguments(),
            ct);
        var secondStart = AspireTestApplication.Start<TAppHost>(
            healthyResourceNames,
            null,
            createAppHostArguments(),
            ct);

        try
        {
            var applications = await Task.WhenAll(firstStart, secondStart).ConfigureAwait(false);
            return new ConcurrentAspireTestApplications(applications[0], applications[1]);
        }
        catch
        {
            await DisposeCompletedApplications(firstStart, secondStart).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Second.DisposeAsync().ConfigureAwait(false);
        await First.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task DisposeCompletedApplications(
        Task<AspireTestApplication> firstStart,
        Task<AspireTestApplication> secondStart)
    {
        if (firstStart.IsCompletedSuccessfully)
        {
            var first = await firstStart.ConfigureAwait(false);
            await first.DisposeAsync().ConfigureAwait(false);
        }

        if (secondStart.IsCompletedSuccessfully)
        {
            var second = await secondStart.ConfigureAwait(false);
            await second.DisposeAsync().ConfigureAwait(false);
        }
    }
}
