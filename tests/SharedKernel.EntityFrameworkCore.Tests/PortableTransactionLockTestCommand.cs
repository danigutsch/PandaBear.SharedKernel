using PandaBear.Mediator;
using PandaBear.Mediator.Transactions;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>A command that declares locks through the persistence-free mediator contract.</summary>
/// <param name="TransactionLocks">The advisory lock names.</param>
internal sealed record PortableTransactionLockTestCommand(IReadOnlyList<string> TransactionLocks) : ICommand<Result>, ITransactionLockDeclaration;
