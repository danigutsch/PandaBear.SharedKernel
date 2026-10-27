namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Cleanup helper that releases PostgreSQL test scenario resources.
/// </summary>
internal static class PostgreSqlScenarioCleanup
{
    /// <summary>
    /// Disposes every resource that is not <see langword="null"/>, in order, and reports all failures together with
    /// <paramref name="operationFailure"/> as <c>PostgreSqlTestCleanup.Run</c> of the shared PostgreSQL test support does.
    /// </summary>
    internal static Task DisposeResources(
        Exception? operationFailure,
        params IAsyncDisposable?[] resources)
    {
        return PostgreSqlTestCleanup.Run(
            operationFailure,
            [.. resources.OfType<IAsyncDisposable>().Select(static resource => (Func<ValueTask>)resource.DisposeAsync)]);
    }
}
