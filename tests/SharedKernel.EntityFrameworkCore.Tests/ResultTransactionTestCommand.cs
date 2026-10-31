using PandaBear.Mediator;
using PandaBear.Mediator.EntityFrameworkCore;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Command whose declared lock names are supplied by the test.
/// </summary>
/// <param name="TransactionLocks">The advisory lock names the command declares.</param>
internal sealed record ResultTransactionTestCommand(IReadOnlyList<string> TransactionLocks) : ICommand<Result>, ITransactionLockDeclaration;
