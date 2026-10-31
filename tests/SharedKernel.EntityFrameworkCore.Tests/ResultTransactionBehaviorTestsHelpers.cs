using Microsoft.EntityFrameworkCore;
using Npgsql;
using PandaBear.Npgsql;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Helpers for <see cref="ResultTransactionBehaviorTests"/>.</summary>
internal static class ResultTransactionBehaviorTestsHelpers
{
    /// <summary>Adds an entity, saves it and returns the given result.</summary>
    /// <param name="context">The context to save through.</param>
    /// <param name="name">The entity name.</param>
    /// <param name="result">The result to return after saving.</param>
    /// <returns>The given result.</returns>
    public static async ValueTask<Result> AddAndSave(TestDbContext context, string name, Result result)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Entities.Add(new TestEntity { Name = name });
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return result;
    }

    /// <summary>Counts committed entities with the name, using a separate context.</summary>
    /// <param name="dataSource">The database data source.</param>
    /// <param name="name">The entity name.</param>
    /// <returns>The committed row count.</returns>
    public static async Task<int> CountNamed(NpgsqlDataSource dataSource, string name)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(dataSource).Options;
        await using var context = new TestDbContext(options);
        return await context.Entities.CountAsync(entity => entity.Name == name, TestContext.Current.CancellationToken);
    }

    /// <summary>Tries to take the session advisory lock for the name on a separate connection and releases it again.</summary>
    /// <param name="dataSource">The database data source.</param>
    /// <param name="name">The lock name.</param>
    /// <returns><see langword="true"/> when no other session held the lock.</returns>
    public static async Task<bool> TryTakeLock(NpgsqlDataSource dataSource, string name)
    {
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
        command.Parameters.AddWithValue("key", PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(name));
        var taken = (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        await using var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock_all()", connection);
        _ = await unlock.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return taken;
    }

    /// <summary>Runs the command in parallel through separate behaviors and reports the highest number of handlers running at once.</summary>
    /// <param name="scenario">The scenario that creates the behaviors.</param>
    /// <param name="command">The command, declaring the contended lock.</param>
    /// <param name="parallelism">How many commands run at once.</param>
    /// <returns>The highest observed handler concurrency.</returns>
    public static async Task<int> MeasureMaxConcurrency(ResultTransactionTestScenario scenario, ResultTransactionTestCommand command, int parallelism)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var running = 0;
        var maxRunning = 0;
        var tasks = Enumerable.Range(0, parallelism).Select(async index =>
        {
            var (behavior, _, scope) = scenario.CreateBehavior();
            using var scopeLease = scope;
            _ = await behavior.Handle(
                command,
                async () =>
                {
                    var current = Interlocked.Increment(ref running);
                    RaiseMax(ref maxRunning, current);
                    await Task.Delay(200, TestContext.Current.CancellationToken);
                    _ = Interlocked.Decrement(ref running);
                    return Result.Ok();
                },
                TestContext.Current.CancellationToken);
        });
        await Task.WhenAll(tasks);
        return maxRunning;
    }

    private static void RaiseMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
