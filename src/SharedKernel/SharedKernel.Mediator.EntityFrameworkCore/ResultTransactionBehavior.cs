using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using PandaBear.Npgsql;
using PandaBear.Results;

namespace PandaBear.Mediator.EntityFrameworkCore;

/// <summary>
/// Runs a command that returns a <see cref="Result"/> or <see cref="Result{T}"/> in one retry-compatible EF Core transaction and commits
/// only when the result is successful.
/// </summary>
/// <remarks>
/// <para>
/// The package assembly is not a mediator module, so the generator does not discover this class. A consumer derives a sealed class in its own
/// module assembly, declares <c>[PipelineOrder(PipelineStage.Transaction)]</c> on it and registers a <see cref="DbContext"/> service.
/// </para>
/// <para>
/// Each attempt, inside <c>CreateExecutionStrategy().ExecuteAsync</c>, clears the change tracker, begins a read committed transaction, takes
/// the locks the request declares through <see cref="ITransactionLockDeclaration"/> at tier 0 with one new
/// <c>PostgreSqlOrderedTransactionLocks</c>, runs the handler, then commits on success and rolls back otherwise (an invalid default result
/// is not successful). Exceptions roll back and propagate so the strategy can retry transient failures.
/// </para>
/// <para>
/// When a transaction is already active the behavior joins it and takes no locks; it throws when that transaction runs at repeatable read,
/// serializable or snapshot isolation, because per-statement snapshots are required for checks that read state after taking locks.
/// <see cref="IAfterRollback{TRequest, TResponse}"/> and <see cref="IAfterCommit{TRequest, TResponse}"/> hooks run once after the last attempt.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The command type.</typeparam>
/// <typeparam name="TResponse">The result type.</typeparam>
public partial class ResultTransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
    where TResponse : struct, IResultLike<TResponse>
{
    private readonly DbContext dbContext;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;

    /// <summary>
    /// Initializes the behavior.
    /// </summary>
    /// <param name="dbContext">The context that owns the transaction boundary.</param>
    /// <param name="scopeFactory">Creates the fresh scope the outcome hooks run in.</param>
    /// <param name="logger">Logs hook failures.</param>
    public ResultTransactionBehavior(DbContext dbContext, IServiceScopeFactory scopeFactory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        this.dbContext = dbContext;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (!dbContext.Database.IsRelational())
        {
            return await next().ConfigureAwait(false);
        }

        if (dbContext.Database.CurrentTransaction is { } active)
        {
            EnsureReadCommitted(active);
            return await next().ConfigureAwait(false);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var (response, committed) = await strategy
            .ExecuteAsync((Behavior: this, Request: request, Next: next), RunAttempt, ct)
            .ConfigureAwait(false);

        await RunHooks(request, response, committed, ct).ConfigureAwait(false);
        return response;
    }

    private static void EnsureReadCommitted(IDbContextTransaction active)
    {
        var isolation = active.GetDbTransaction().IsolationLevel;
        if (isolation is IsolationLevel.RepeatableRead or IsolationLevel.Serializable or IsolationLevel.Snapshot)
        {
            throw new InvalidOperationException(
                $"The active transaction runs at {isolation} isolation. Command transactions require read committed so each statement sees fresh data.");
        }
    }

    private static async Task<(TResponse Response, bool Committed)> RunAttempt(
        (ResultTransactionBehavior<TRequest, TResponse> Behavior, TRequest Request, RequestHandlerContinuation<TResponse> Next) state,
        CancellationToken ct)
    {
        var dbContext = state.Behavior.dbContext;
        dbContext.ChangeTracker.Clear();
        try
        {
            var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
            await using var _ = transaction.ConfigureAwait(false);

            await AcquireDeclaredLocks(dbContext, transaction, state.Request, ct).ConfigureAwait(false);

            var response = await state.Next().ConfigureAwait(false);
            if (response.IsSuccess)
            {
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return (response, true);
            }

            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return (response, false);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private static async ValueTask AcquireDeclaredLocks(
        DbContext dbContext,
        IDbContextTransaction transaction,
        TRequest request,
        CancellationToken ct)
    {
        if (request is not ITransactionLockDeclaration declaration)
        {
            return;
        }

        if (dbContext.Database.GetDbConnection() is not NpgsqlConnection connection
            || transaction.GetDbTransaction() is not NpgsqlTransaction npgsqlTransaction)
        {
            throw new InvalidOperationException("Declared transaction locks require the Npgsql provider.");
        }

        var locks = new PostgreSqlOrderedTransactionLocks(connection, npgsqlTransaction);
        await locks.Acquire(declaration.TransactionLocks.Select(static name => new PostgreSqlTransactionLockRequest(0, name)), ct).ConfigureAwait(false);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failing outcome hook is logged and must never change the committed or failed result.")]
    private async ValueTask RunHooks(TRequest request, TResponse response, bool committed, CancellationToken ct)
    {
        var scope = scopeFactory.CreateAsyncScope();
        await using var _ = scope.ConfigureAwait(false);

        if (committed)
        {
            foreach (var hook in scope.ServiceProvider.GetServices<IAfterCommit<TRequest, TResponse>>())
            {
                try
                {
                    await hook.Handle(request, response, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogHookFailure(logger, typeof(TRequest).Name, hook.GetType().Name, nameof(IAfterCommit<,>), ex.GetType().Name);
                }
            }

            return;
        }

        foreach (var hook in scope.ServiceProvider.GetServices<IAfterRollback<TRequest, TResponse>>())
        {
            try
            {
                await hook.Handle(request, response, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHookFailure(logger, typeof(TRequest).Name, hook.GetType().Name, nameof(IAfterRollback<,>), ex.GetType().Name);
            }
        }
    }

    [LoggerMessage(1, LogLevel.Error, "Command {Request}: {HookKind} hook {HookType} failed and was ignored. Failure type: {FailureType}.")]
    private static partial void LogHookFailure(ILogger logger, string request, string hookKind, string hookType, string failureType);
}
