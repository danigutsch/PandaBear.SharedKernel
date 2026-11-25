using static SharedKernel.Npgsql.Tests.PostgreSqlJobSchedulerTestData;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Verifies durable scheduler controls and metadata reads against PostgreSQL.</summary>
/// <param name="fixture">The isolated PostgreSQL server fixture.</param>
public sealed class PostgreSqlJobSchedulerControlTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    private const string JobName = "retention";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task Paused_jobs_hold_requests_across_restart_and_manual_completion_preserves_recurrence()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scheduler = await CreateScheduler(DataSource, ct);
        var now = await DatabaseTime(DataSource, ct);
        var originalNext = now.AddHours(2);
        await scheduler.Register(JobName, originalNext, TimeSpan.FromDays(1), ct);
        var paused = await scheduler.SetPaused(JobName, true, ct);
        var request = Guid.NewGuid();
        await using var secondHost = NpgsqlDataSource.Create(ConnectionString);
        var restarted = new PostgreSqlJobScheduler(secondHost, Schema);

        // Act
        var requests = await Task.WhenAll(scheduler.Trigger(JobName, request, ct), restarted.Trigger(JobName, request, ct));
        var registered = await restarted.Register(JobName, now.AddMinutes(-1), null, ct);
        var pausedJob = await restarted.ReadJob(JobName, ct);
        var inactive = await restarted.Acquire(LeaseDuration, ct);
        var queuedHistory = await restarted.ReadHistory(JobName, null, 2, ct);
        await restarted.SetPaused(JobName, false, ct);
        var claimed = await restarted.Acquire(LeaseDuration, ct);
        var run = claimed.ShouldNotBeNull();
        await restarted.Complete(run, PostgreSqlScheduleRunStatus.Succeeded, now, ct);
        var next = await restarted.ReadJob(JobName, ct);
        var duplicate = await restarted.Trigger(JobName, request, ct);
        var another = await restarted.Acquire(LeaseDuration, ct);

        // Assert
        paused.ShouldBeTrue();
        requests.ShouldBe([true, true]);
        registered.ShouldBeFalse();
        pausedJob.ShouldNotBeNull().IsPaused.ShouldBeTrue();
        inactive.ShouldBeNull();
        queuedHistory.Runs.ShouldBeEmpty();
        run.RunId.ShouldBe(request);
        run.Kind.ShouldBe(PostgreSqlJobRunKind.Manual);
        next.ShouldNotBeNull().NextDueAt.ShouldBe(originalNext);
        duplicate.ShouldBeTrue();
        another.ShouldBeNull();
    }

    [Fact]
    public async Task Retry_records_one_request_and_reuses_the_existing_history_contract()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scheduler = await CreateScheduler(DataSource, ct);
        var now = await DatabaseTime(DataSource, ct);
        await scheduler.Register(JobName, now.AddHours(-1), null, ct);
        var acquired = await scheduler.Acquire(LeaseDuration, ct);
        var failed = acquired.ShouldNotBeNull();
        var callerDue = now.AddHours(1);
        await scheduler.Complete(failed, PostgreSqlScheduleRunStatus.Failed, callerDue, ct);
        var request = Guid.NewGuid();

        // Act
        var requested = await scheduler.Retry(JobName, failed.RunId, request, ct);
        var repeated = await scheduler.Retry(JobName, failed.RunId, request, ct);
        var unrelated = await scheduler.Trigger(JobName, request, ct);
        var anotherRetry = await scheduler.Retry(JobName, failed.RunId, Guid.NewGuid(), ct);
        var claimed = await scheduler.Acquire(LeaseDuration, ct);
        var retry = claimed.ShouldNotBeNull();
        var active = await scheduler.ReadActive(JobName, null, 1, ct);
        await scheduler.Complete(retry, PostgreSqlScheduleRunStatus.Succeeded, null, ct);
        var first = await scheduler.ReadHistory(JobName, null, 1, ct);
        var second = await scheduler.ReadHistory(JobName, first.Next, 1, ct);
        var job = await scheduler.ReadJob(JobName, ct);

        // Assert
        requested.ShouldBeTrue();
        repeated.ShouldBeTrue();
        unrelated.ShouldBeFalse();
        anotherRetry.ShouldBeFalse();
        retry.RunId.ShouldBe(request);
        retry.Kind.ShouldBe(PostgreSqlJobRunKind.Retry);
        active.Runs.ShouldHaveSingleItem().Status.ShouldBe(PostgreSqlScheduleRunStatus.Running);
        first.Next.ShouldNotBeNull();
        first.Runs.ShouldHaveSingleItem().RunId.ShouldBe(request);
        var previous = second.Runs.ShouldHaveSingleItem();
        previous.RunId.ShouldBe(failed.RunId);
        previous.Status.ShouldBe(PostgreSqlScheduleRunStatus.RetryScheduled);
        previous.RetryAt.ShouldNotBeNull();
        second.Next.ShouldBeNull();
        job.ShouldNotBeNull().NextDueAt.ShouldBe(callerDue);
    }

    [Fact]
    public async Task Caller_rollback_restores_pause_queue_and_failed_outcome_together()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scheduler = await CreateScheduler(DataSource, ct);
        var now = await DatabaseTime(DataSource, ct);
        await scheduler.Register(JobName, now.AddHours(-1), null, ct);
        var acquired = await scheduler.Acquire(LeaseDuration, ct);
        var failed = acquired.ShouldNotBeNull();
        await scheduler.Complete(failed, PostgreSqlScheduleRunStatus.Failed, null, ct);
        var triggerId = Guid.NewGuid();
        var retryId = Guid.NewGuid();
        await using var connection = await DataSource.OpenConnectionAsync(ct);

        // Act
        await using (var transaction = await connection.BeginTransactionAsync(ct))
        {
            var paused = await scheduler.SetPaused(JobName, true, transaction, ct);
            var triggered = await scheduler.Trigger(JobName, triggerId, transaction, ct);
            var retried = await scheduler.Retry(JobName, failed.RunId, retryId, transaction, ct);
            paused.ShouldBeTrue();
            triggered.ShouldBeTrue();
            retried.ShouldBeTrue();
            await transaction.RollbackAsync(ct);
        }

        var job = await scheduler.ReadJob(JobName, ct);
        var history = await scheduler.ReadHistory(JobName, null, 2, ct);
        var none = await scheduler.Acquire(LeaseDuration, ct);
        await using (var transaction = await connection.BeginTransactionAsync(ct))
        {
            await scheduler.Trigger(JobName, triggerId, transaction, ct);
            await transaction.CommitAsync(ct);
        }

        var committed = await scheduler.Acquire(LeaseDuration, ct);

        // Assert
        job.ShouldNotBeNull().IsPaused.ShouldBeFalse();
        history.Runs.ShouldHaveSingleItem().Status.ShouldBe(PostgreSqlScheduleRunStatus.Failed);
        none.ShouldBeNull();
        committed.ShouldNotBeNull().RunId.ShouldBe(triggerId);
    }

    [Fact]
    public async Task Cancelled_acquisition_keeps_the_due_execution_available()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scheduler = await CreateScheduler(DataSource, ct);
        var now = await DatabaseTime(DataSource, ct);
        await scheduler.Register(JobName, now.AddMinutes(-1), null, ct);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await cancellation.CancelAsync();

        // Act
        Func<Task> acquire = () => scheduler.Acquire(LeaseDuration, cancellation.Token);
        await acquire.ShouldThrow<OperationCanceledException>();
        var succeedingWorker = await scheduler.Acquire(LeaseDuration, ct);

        // Assert
        succeedingWorker.ShouldNotBeNull().JobName.ShouldBe(JobName);
    }
}
