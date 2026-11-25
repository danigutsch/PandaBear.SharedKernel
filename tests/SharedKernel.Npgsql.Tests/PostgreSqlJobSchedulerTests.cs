using static SharedKernel.Npgsql.Tests.PostgreSqlJobSchedulerTestData;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Tests real database coordination and lease fencing across scheduler instances.</summary>
/// <param name="fixture">The isolated PostgreSQL database fixture.</param>
public sealed class PostgreSqlJobSchedulerTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    private const string JobName = "draft-retention";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task Competing_hosts_acquire_a_due_job_only_once()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var first = await CreateScheduler(DataSource, ct);
        await using var otherHost = NpgsqlDataSource.Create(ConnectionString);
        var second = new PostgreSqlJobScheduler(otherHost, Schema);
        var now = await DatabaseTime(DataSource, ct);
        var registered = await first.Register(JobName, now.AddMinutes(-1), TimeSpan.FromDays(1), ct);

        // Act
        var results = await Task.WhenAll(first.Acquire(LeaseDuration, ct), second.Acquire(LeaseDuration, ct));
        var competingRetry = await second.Acquire(LeaseDuration, ct);

        // Assert
        registered.ShouldBeTrue();
        var owned = results.OfType<PostgreSqlJobLease>().ShouldHaveSingleItem();
        owned.JobName.ShouldBe(JobName);
        owned.RunId.ShouldNotBe(Guid.Empty);
        owned.Fence.ShouldNotBe(Guid.Empty);
        competingRetry.ShouldBeNull();
    }

    [Fact]
    public async Task Acquire_orders_tied_due_jobs_by_stable_name()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scheduler = await CreateScheduler(DataSource, ct);
        var now = await DatabaseTime(DataSource, ct);
        await scheduler.Register("retention-b", now.AddMinutes(-1), null, ct);
        await scheduler.Register("retention-a", now.AddMinutes(-1), null, ct);

        // Act
        var first = await scheduler.Acquire(LeaseDuration, ct);
        var second = await scheduler.Acquire(LeaseDuration, ct);

        // Assert
        first.ShouldNotBeNull().JobName.ShouldBe("retention-a");
        second.ShouldNotBeNull().JobName.ShouldBe("retention-b");
    }

    [Fact]
    public async Task Expired_owner_cannot_commit_and_restart_reacquires_the_same_run_with_a_new_fence()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var first = await CreateScheduler(DataSource, ct);
        var now = await DatabaseTime(DataSource, ct);
        await first.Register(JobName, now.AddMinutes(-1), TimeSpan.FromDays(1), ct);
        var lostClaim = await first.Acquire(LeaseDuration, ct);
        var lost = lostClaim.ShouldNotBeNull();
        await using var expire = DataSource.CreateCommand(
            """
            UPDATE "Job Scheduling".scheduler_executions
            SET observed_until = pg_catalog.clock_timestamp()
            WHERE run_id = @run_id
            """);
        expire.Parameters.AddWithValue("run_id", lost.RunId);
        await expire.ExecuteNonQueryAsync(ct);
        await using var restartedHost = NpgsqlDataSource.Create(ConnectionString);
        var restarted = new PostgreSqlJobScheduler(restartedHost, Schema);

        // Act
        var lateRenewal = await first.Renew(lost, LeaseDuration, ct);
        var lateCompletion = await first.Complete(lost, PostgreSqlScheduleRunStatus.Succeeded, null, ct);
        var recoveredClaim = await restarted.Acquire(LeaseDuration, ct);
        var recovered = recoveredClaim.ShouldNotBeNull();
        var supersededCompletion = await first.Complete(lost, PostgreSqlScheduleRunStatus.Succeeded, null, ct);
        var currentCompletion = await restarted.Complete(recovered, PostgreSqlScheduleRunStatus.Succeeded, null, ct);

        // Assert
        lateRenewal.ShouldBeFalse();
        lateCompletion.ShouldBeFalse();
        recovered.RunId.ShouldBe(lost.RunId);
        recovered.Fence.ShouldNotBe(lost.Fence);
        recovered.ScheduledFor.ShouldBe(lost.ScheduledFor);
        supersededCompletion.ShouldBeFalse();
        currentCompletion.ShouldBeTrue();
    }

    [Fact]
    public async Task Completion_uses_the_database_time_and_reregistration_preserves_next_due()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scheduler = await CreateScheduler(DataSource, ct);
        var delay = TimeSpan.FromDays(1);
        var now = await DatabaseTime(DataSource, ct);
        await scheduler.Register(JobName, now.AddHours(-2), delay, ct);
        var leaseClaim = await scheduler.Acquire(LeaseDuration, ct);
        var lease = leaseClaim.ShouldNotBeNull();

        // Act
        var completed = await scheduler.Complete(lease, PostgreSqlScheduleRunStatus.Failed, null, ct);
        var registeredAgain = await scheduler.Register(JobName, now.AddHours(-2), TimeSpan.FromMinutes(1), ct);
        var repeated = await scheduler.Acquire(LeaseDuration, ct);
        await using var query = DataSource.CreateCommand(
            """
            SELECT job.next_due_at - execution.finished_at
            FROM "Job Scheduling".scheduler_jobs AS job
            JOIN "Job Scheduling".scheduler_executions AS execution USING (job_name)
            WHERE execution.run_id = @run_id
            """);
        query.Parameters.AddWithValue("run_id", lease.RunId);
        var actualDelay = await query.ExecuteScalarAsync(ct);

        // Assert
        completed.ShouldBeTrue();
        registeredAgain.ShouldBeFalse();
        repeated.ShouldBeNull();
        actualDelay.ShouldBe(delay);
    }

    [Fact]
    public async Task Renewal_extends_the_fence_using_database_time_without_shortening_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scheduler = await CreateScheduler(DataSource, ct);
        var now = await DatabaseTime(DataSource, ct);
        await scheduler.Register(JobName, now.AddMinutes(-1), null, ct);
        var leaseClaim = await scheduler.Acquire(LeaseDuration, ct);
        var lease = leaseClaim.ShouldNotBeNull();
        var beforeRenew = await DatabaseTime(DataSource, ct);
        var extendedDuration = TimeSpan.FromMinutes(20);

        // Act
        var renewed = await scheduler.Renew(lease, extendedDuration, ct);
        var afterRenew = await DatabaseTime(DataSource, ct);
        await scheduler.Renew(lease, LeaseDuration, ct);
        await using var query = DataSource.CreateCommand(
            """
            SELECT observed_until
            FROM "Job Scheduling".scheduler_executions
            WHERE run_id = @run_id
            """);
        query.Parameters.AddWithValue("run_id", lease.RunId);
        var until = (DateTime)(await query.ExecuteScalarAsync(ct))!;

        // Assert
        renewed.ShouldBeTrue();
        until.ShouldBeGreaterThanOrEqualTo(beforeRenew.UtcDateTime.Add(extendedDuration));
        until.ShouldBeLessThanOrEqualTo(afterRenew.UtcDateTime.Add(extendedDuration));
    }
}
