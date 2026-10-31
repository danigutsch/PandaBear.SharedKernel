using Microsoft.EntityFrameworkCore;
using PandaBear.Mediator;
using PandaBear.Mediator.EntityFrameworkCore;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Command whose declared lock names are supplied by the test.
/// </summary>
/// <param name="LockNames">The advisory lock names the command declares.</param>
internal sealed record ResultTransactionTestCommand(IReadOnlyList<string> LockNames) : ICommand<Result>, ITransactionLockDeclaration
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetTransactionLocks(DbContext dbContext, CancellationToken ct) => ValueTask.FromResult(LockNames);
}
