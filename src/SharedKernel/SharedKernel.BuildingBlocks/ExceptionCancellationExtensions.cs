using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// Provides exception helpers for cooperative cancellation handling.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "C# extension block members are emitted as compiler implementation details.")]
public static class ExceptionCancellationExtensions
{
    extension(Exception exception)
    {
        /// <summary>
        /// Returns whether the exception should be handled as a failure instead of cooperative cancellation.
        /// </summary>
        /// <param name="ct">The operation cancellation token.</param>
        /// <returns>
        /// <see langword="true" /> when the exception is not a cooperative cancellation for <paramref name="ct" />;
        /// otherwise, <see langword="false" />.
        /// </returns>
        public bool ShouldHandleAsFailure(CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception is not OperationCanceledException || !ct.IsCancellationRequested;
        }
    }
}
