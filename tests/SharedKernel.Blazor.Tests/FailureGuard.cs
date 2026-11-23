namespace PandaBear.Blazor.Tests;

/// <summary>
/// Bounds what a test waits for, so a regression that never completes the awaited work fails the test instead of hanging the run. The
/// bound is never what makes a test pass: every awaited task completes at once, or as soon as the test releases its gate or advances
/// its clock, when the code is right.
/// </summary>
internal static class FailureGuard
{
    /// <summary>The longest a test waits before it fails.</summary>
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    /// <summary>Waits for the task, at most <see cref="Limit"/> and only until the test is cancelled.</summary>
    /// <param name="task">The awaited work.</param>
    /// <returns>A task that completes with the work, or fails with a <see cref="TimeoutException"/> when the work does not complete in time.</returns>
    public static Task Guarded(this Task task) => task.WaitAsync(Limit, Xunit.TestContext.Current.CancellationToken);
}
