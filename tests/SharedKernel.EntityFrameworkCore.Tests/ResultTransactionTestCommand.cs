using SharedKernel.Mediator;
using SharedKernel.Mediator.EntityFrameworkCore;
using SharedKernel.Results;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>
/// Command whose declared lock names are supplied by the test.
/// </summary>
/// <param name="LockNames">The advisory lock names the command declares.</param>
#pragma warning disable SKMED001 // This fixture invokes the transaction behavior directly.
internal sealed record ResultTransactionTestCommand(IReadOnlyList<string> LockNames) : ICommand<Result>, ITransactionLockDeclaration
{
    IReadOnlyList<string> ITransactionLockDeclaration.TransactionLocks => LockNames;
}
#pragma warning restore SKMED001
