namespace PandaBear.Mediator.EntityFrameworkCore;

/// <summary>
/// Runs after <see cref="ResultTransactionBehavior{TRequest, TResponse}"/> rolled a command back because its result was not successful.
/// </summary>
/// <remarks>
/// Hooks run once, after the last attempt, outside the rolled back transaction, from a fresh dependency injection scope. They run for an
/// unsuccessful result only; an exception propagates without running them. Use them for records that must outlive the rollback, such as
/// rejection audits. A hook failure is logged and never changes the returned result.
/// </remarks>
/// <typeparam name="TRequest">The command type.</typeparam>
/// <typeparam name="TResponse">The command response type.</typeparam>
public interface IAfterRollback<in TRequest, in TResponse>
{
    /// <summary>
    /// Handles the rolled back command.
    /// </summary>
    /// <param name="request">The command.</param>
    /// <param name="response">The unsuccessful response.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the hook is done.</returns>
    ValueTask Handle(TRequest request, TResponse response, CancellationToken ct);
}
