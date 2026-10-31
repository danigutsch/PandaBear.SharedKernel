namespace PandaBear.Mediator.EntityFrameworkCore;

/// <summary>
/// Implemented by a command that needs PostgreSQL transaction-scoped advisory locks before its handler runs.
/// </summary>
/// <remarks>
/// <see cref="ResultTransactionBehavior{TRequest, TResponse}"/> reads <see cref="TransactionLocks"/> after the transaction begins and
/// takes the locks at tier 0 through one <c>PostgreSqlOrderedTransactionLocks</c> instance per attempt. Handlers never acquire
/// these locks themselves. The behavior takes no locks when it joins an already active transaction. The declaration carries no
/// persistence type, so the application layer can implement it without referencing EF Core.
/// </remarks>
public interface ITransactionLockDeclaration
{
    /// <summary>
    /// Gets the names of the advisory locks the command needs.
    /// </summary>
    /// <value>The lock names; duplicates are collapsed and ordering is handled by the lock helper.</value>
    IReadOnlyList<string> TransactionLocks
    {
        get;
    }
}
