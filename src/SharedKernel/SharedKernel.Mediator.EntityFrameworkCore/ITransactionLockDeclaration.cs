using Microsoft.EntityFrameworkCore;

namespace PandaBear.Mediator.EntityFrameworkCore;

/// <summary>
/// Implemented by a command that needs PostgreSQL transaction-scoped advisory locks before its handler runs.
/// </summary>
/// <remarks>
/// <see cref="ResultTransactionBehavior{TRequest, TResponse}"/> calls <see cref="GetTransactionLocks"/> after the transaction begins and
/// takes the returned locks at tier 0 through one <c>PostgreSqlOrderedTransactionLocks</c> instance per attempt. Handlers never acquire
/// these locks themselves. The behavior takes no locks when it joins an already active transaction.
/// </remarks>
public interface ITransactionLockDeclaration
{
    /// <summary>
    /// Gets the names of the advisory locks the command needs, resolved inside the transaction.
    /// </summary>
    /// <param name="dbContext">The context that owns the open transaction, for lookups such as resolving an owning aggregate identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The lock names; duplicates are collapsed and ordering is handled by the lock helper.</returns>
    ValueTask<IReadOnlyList<string>> GetTransactionLocks(DbContext dbContext, CancellationToken ct);
}
