namespace SharedKernel.BuildingBlocks;

/// <summary>
/// Provides exception helpers for cooperative cancellation handling.
/// </summary>
public static class ExceptionCancellationExtensions
{
    /// <summary>
    /// Returns whether the exception should be handled as a failure instead of cooperative cancellation.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <param name="ct">The operation cancellation token.</param>
    /// <returns>
    /// <see langword="true" /> when the exception is not a cooperative cancellation for <paramref name="ct" />;
    /// otherwise, <see langword="false" />.
    /// </returns>
    public static bool ShouldHandleAsFailure(this Exception exception, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is not OperationCanceledException || !ct.IsCancellationRequested;
    }
}
