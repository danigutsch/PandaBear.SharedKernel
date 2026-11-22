using PandaBear.Mediator;
using PandaBear.Mediator.EntityFrameworkCore;
using PandaBear.Results;
using PortableDeclaration = PandaBear.Mediator.Transactions.ITransactionLockDeclaration;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>A command declaring locks through both the current and compatible EF contract.</summary>
/// <param name="PortableLocks">Names from the persistence-free contract.</param>
/// <param name="LegacyLocks">Names from the original EF contract.</param>
internal sealed record DualTransactionLockTestCommand(IReadOnlyList<string> PortableLocks, IReadOnlyList<string> LegacyLocks)
    : ICommand<Result>, PortableDeclaration, ITransactionLockDeclaration
{
    IReadOnlyList<string> PortableDeclaration.TransactionLocks => PortableLocks;

    IReadOnlyList<string> ITransactionLockDeclaration.TransactionLocks => LegacyLocks;
}
