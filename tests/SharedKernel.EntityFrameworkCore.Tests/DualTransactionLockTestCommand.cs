using SharedKernel.Mediator;
using SharedKernel.Mediator.EntityFrameworkCore;
using SharedKernel.Results;
using PortableDeclaration = SharedKernel.Mediator.Transactions.ITransactionLockDeclaration;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>A command declaring locks through both the current and compatible EF contract.</summary>
/// <param name="PortableLocks">Names from the persistence-free contract.</param>
/// <param name="LegacyLocks">Names from the original EF contract.</param>
#pragma warning disable SKMED001 // This fixture invokes the transaction behavior directly.
internal sealed record DualTransactionLockTestCommand(IReadOnlyList<string> PortableLocks, IReadOnlyList<string> LegacyLocks)
    : ICommand<Result>, PortableDeclaration, ITransactionLockDeclaration
{
    IReadOnlyList<string> PortableDeclaration.TransactionLocks => PortableLocks;

    IReadOnlyList<string> ITransactionLockDeclaration.TransactionLocks => LegacyLocks;
}
#pragma warning restore SKMED001
