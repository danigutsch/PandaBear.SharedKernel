namespace PandaBear.Testing.Tests;

/// <summary>Runs work outside any xUnit test, as a server's request threads or background work do.</summary>
internal static class OutsideAnyTest
{
    /// <summary>Runs <paramref name="action"/> on the thread pool without the calling test's execution context.</summary>
    /// <param name="action">The work.</param>
    /// <returns>A task that completes when the work has run.</returns>
    internal static Task Run(Action action)
    {
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(() =>
            {
                TestContext.Current.TestOutputHelper.ShouldBeNull("the work must run outside any test");
                action();
            });
        }
    }
}
