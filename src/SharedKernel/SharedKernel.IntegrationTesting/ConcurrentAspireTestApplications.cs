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
    /// <param name="startApplication">Starts an isolated AppHost application instance.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The concurrently started applications.</returns>
    public static async Task<ConcurrentAspireTestApplications> Start(
        Func<CancellationToken, Task<AspireTestApplication>> startApplication,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(startApplication);

        var firstStart = startApplication(ct);
        var secondStart = startApplication(ct);

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
