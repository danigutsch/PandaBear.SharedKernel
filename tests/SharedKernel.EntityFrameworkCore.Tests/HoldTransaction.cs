using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Microsoft.EntityFrameworkCore;
using PandaBear.DispositionHold;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// One scope of the hold store, guard and context, optionally inside an open transaction, standing for one caller.
/// </summary>
internal sealed class HoldTransaction : IAsyncDisposable
{
    private readonly AsyncServiceScope scope;
    private readonly DispositionHoldTestDbContext dbContext;

    private HoldTransaction(AsyncServiceScope scope)
    {
        this.scope = scope;
        dbContext = scope.ServiceProvider.GetRequiredService<DispositionHoldTestDbContext>();
        Store = scope.ServiceProvider.GetRequiredService<IDispositionHoldStore>();
        Guard = scope.ServiceProvider.GetRequiredService<IDispositionHoldGuard>();
    }

    /// <summary>Gets the hold store of this caller.</summary>
    public IDispositionHoldStore Store
    {
        get;
    }

    /// <summary>Gets the hold guard of this caller.</summary>
    public IDispositionHoldGuard Guard
    {
        get;
    }

    /// <summary>Opens a caller, beginning a transaction when asked.</summary>
    public static async ValueTask<HoldTransaction> Open(
        IServiceProvider services,
        bool beginTransaction,
        IsolationLevel isolationLevel,
        CancellationToken ct)
    {
        var holdTransaction = new HoldTransaction(services.CreateAsyncScope());
        if (beginTransaction)
        {
            await holdTransaction.dbContext.Database.BeginTransactionAsync(isolationLevel, ct);
        }

        return holdTransaction;
    }

    /// <summary>Reads any row inside the open transaction, so the transaction has already run a statement.</summary>
    public async ValueTask RunAnyQuery(CancellationToken ct) =>
        _ = await dbContext.Database.SqlQuery<int>($"SELECT 1 AS \"Value\"").ToListAsync(ct);

    /// <summary>Saves staged changes and commits the open transaction.</summary>
    public async ValueTask Commit(CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
        await dbContext.Database.CommitTransactionAsync(ct);
    }

    /// <summary>Checks the scopes in a fresh transaction each round, rolling back between rounds, to provoke lock crossings.</summary>
    public async Task CheckRepeatedly(
        IReadOnlyCollection<DispositionScope> scopes,
        DateTimeOffset at,
        int rounds,
        CancellationToken ct)
    {
        await Task.Yield();
        for (var round = 0; round < rounds; round++)
        {
            _ = await Guard.Check(scopes, at, ct);
            await dbContext.Database.RollbackTransactionAsync(ct);
            await dbContext.Database.BeginTransactionAsync(ct);
        }
    }

    /// <summary>Rolls the open transaction back and begins a new one, as a retry attempt does.</summary>
    public async ValueTask Restart(CancellationToken ct)
    {
        await dbContext.Database.RollbackTransactionAsync(ct);
        await dbContext.Database.BeginTransactionAsync(ct);
    }

    /// <summary>Rolls the open transaction back.</summary>
    public async ValueTask Rollback(CancellationToken ct) => await dbContext.Database.RollbackTransactionAsync(ct);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
        await scope.DisposeAsync();
    }
}
