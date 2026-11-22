namespace PandaBear.Mediator.Transactions;

/// <summary>A command's declaration of the named locks it needs before its handler runs in a transaction.</summary>
/// <remarks>The consuming transaction behavior owns acquisition, ordering, and release.</remarks>
public interface ITransactionLockDeclaration
{
    /// <summary>Gets the command's transaction lock names.</summary>
    IReadOnlyList<string> TransactionLocks { get; }
}
