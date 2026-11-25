using SharedKernel.Mediator;
using SharedKernel.Mediator.Transactions;
using SharedKernel.Results;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>A command that declares locks through the persistence-free mediator contract.</summary>
/// <param name="TransactionLocks">The advisory lock names.</param>
#pragma warning disable SKMED001 // This fixture invokes the transaction behavior directly.
internal sealed record PortableTransactionLockTestCommand(IReadOnlyList<string> TransactionLocks) : ICommand<Result>, ITransactionLockDeclaration;
#pragma warning restore SKMED001
