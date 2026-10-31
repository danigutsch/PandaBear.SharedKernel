namespace PandaBear.Mediator.EntityFrameworkCore;

/// <summary>
/// Runs after <see cref="ResultTransactionBehavior{TRequest, TResponse}"/> committed a command.
/// </summary>
/// <remarks>
/// Hooks run once, outside the committed transaction, from a fresh dependency injection scope. A hook failure is logged and never changes the
/// committed response. Only side effects that need no atomicity belong here; state that must be atomic with the command belongs in the
/// handler or in domain event handlers that run inside the transaction.
/// </remarks>
/// <typeparam name="TRequest">The command type.</typeparam>
/// <typeparam name="TResponse">The command response type.</typeparam>
public interface IAfterCommit<in TRequest, in TResponse>
{
    /// <summary>
    /// Handles the committed command.
    /// </summary>
    /// <param name="request">The command.</param>
    /// <param name="response">The successful response.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the hook is done.</returns>
    ValueTask Handle(TRequest request, TResponse response, CancellationToken ct);
}
