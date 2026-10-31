using PandaBear.Mediator.EntityFrameworkCore;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Records outcome hook calls and can be told to fail.
/// </summary>
internal sealed class ResultTransactionTestHooks
    : IAfterCommit<ResultTransactionTestCommand, Result>, IAfterRollback<ResultTransactionTestCommand, Result>
{
    private readonly List<string> calls = [];

    /// <summary>Gets the recorded hook names in call order.</summary>
    public IReadOnlyList<string> Calls => calls;

    /// <summary>Gets or sets a value indicating whether the hooks throw after recording the call.</summary>
    public bool Throw
    {
        get; set;
    }

    ValueTask IAfterCommit<ResultTransactionTestCommand, Result>.Handle(ResultTransactionTestCommand request, Result response, CancellationToken ct) =>
        Record("commit");

    ValueTask IAfterRollback<ResultTransactionTestCommand, Result>.Handle(ResultTransactionTestCommand request, Result response, CancellationToken ct) =>
        Record("rollback");

    private ValueTask Record(string name)
    {
        calls.Add(name);
        return Throw ? throw new InvalidOperationException("hook failed") : ValueTask.CompletedTask;
    }
}
