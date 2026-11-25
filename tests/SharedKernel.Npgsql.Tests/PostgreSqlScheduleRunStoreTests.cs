using static SharedKernel.Npgsql.Tests.PostgreSqlScheduleRunStoreTestData;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Exercises durable scheduling observations on an isolated PostgreSQL database.</summary>
/// <param name="fixture">The fixture that supplies the per-test database.</param>
public sealed class PostgreSqlScheduleRunStoreTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    private const string Job = "retention";
    private static readonly TimeSpan ObservationWindow = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task Restart_preserves_durable_results_and_expires_only_the_unobserved_host_run()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await CreateTable(DataSource, ct);
        var interrupted = Guid.NewGuid();
        var ended = Guid.NewGuid();
        await using (var firstHost = NpgsqlDataSource.Create(ConnectionString))
        {
            var firstStore = new PostgreSqlScheduleRunStore(firstHost, Schema, Table);
            _ = await firstStore.Start(Job, interrupted, null, ObservationWindow, ct);
            _ = await firstStore.Start(Job, ended, null, ObservationWindow, ct);
            _ = await firstStore.Complete(Job, ended, PostgreSqlScheduleRunStatus.TerminalFailure, null, ct);
        }

        await using var secondHost = NpgsqlDataSource.Create(ConnectionString);
        var secondStore = new PostgreSqlScheduleRunStore(secondHost, Schema, Table);
        var running = Guid.NewGuid();
        _ = await secondStore.Start(Job, running, null, ObservationWindow, ct);
        var beforeExpiry = await secondStore.ReadPage(Job, null, 100, ct);
        await using var expire = DataSource.CreateCommand(
            """
            UPDATE "Scheduling ops"."Run history"
            SET observed_until = pg_catalog.statement_timestamp()
            WHERE run_id = @run_id
            """);
        expire.Parameters.AddWithValue("run_id", interrupted);
        _ = await expire.ExecuteNonQueryAsync(ct);

        // Act
        await using var restartedHost = NpgsqlDataSource.Create(ConnectionString);
        var restartedStore = new PostgreSqlScheduleRunStore(restartedHost, Schema, Table);
        var afterExpiry = await restartedStore.ReadPage(Job, null, 100, ct);

        // Assert
        beforeExpiry.Runs.Where(row => row.RunId == interrupted).ShouldHaveSingleItem().Status.ShouldBe(PostgreSqlScheduleRunStatus.Running);
        afterExpiry.Runs.Select(row => (row.RunId, row.Status)).ShouldBe(
        [
            (running, PostgreSqlScheduleRunStatus.Running),
            (ended, PostgreSqlScheduleRunStatus.TerminalFailure),
            (interrupted, PostgreSqlScheduleRunStatus.Unknown),
        ]);
        afterExpiry.Runs[^1].FinishedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Pages_exact_jobs_in_timestamp_and_uuid_order_without_reading_private_columns()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await CreateTable(DataSource, ct);
        await using var seed = DataSource.CreateCommand(
            """
            INSERT INTO "Scheduling ops"."Run history"
                (job_name, run_id, started_at, observed_until, scheduled_for)
            SELECT 'retention', ('00000000-0000-0000-0000-' || lpad(value::text, 12, '0'))::uuid,
                '2026-01-02T00:00:00Z', '2026-01-02T00:00:01Z', '2026-01-01T23:59:00Z'
            FROM generate_series(1, 101) AS batch(value);
            INSERT INTO "Scheduling ops"."Run history" (job_name, run_id, started_at, observed_until)
            VALUES ('Retention', '00000000-0000-0000-0000-000000000102', '2026-01-02T00:00:00Z', '2026-01-02T00:00:01Z');
            CREATE VIEW "Scheduling ops"."Safe report" AS
                SELECT job_name, run_id, started_at, observed_until, scheduled_for, finished_at, outcome, retry_at,
                    pg_catalog.current_setting('scheduling.tests_private_arguments') AS private_arguments,
                    pg_catalog.current_setting('scheduling.tests_raw_error') AS raw_error,
                    pg_catalog.current_setting('scheduling.tests_credentials') AS credentials
                FROM "Scheduling ops"."Run history";
            """);
        _ = await seed.ExecuteNonQueryAsync(ct);
        var store = new PostgreSqlScheduleRunStore(DataSource, Schema, "Safe report");

        // Act
        await using var privateRead = DataSource.CreateCommand("""SELECT credentials FROM "Scheduling ops"."Safe report" LIMIT 1""");
        Func<Task> readPrivate = () => privateRead.ExecuteScalarAsync(ct);
        var privateFailure = await readPrivate.ShouldThrow<PostgresException>();
        var first = await store.ReadPage(Job, null, 100, ct);
        var second = await store.ReadPage(Job, first.Next, 100, ct);

        // Assert
        privateFailure.SqlState.ShouldBe(PostgresErrorCodes.UndefinedObject);
        first.Runs.Select(row => row.RunId).ShouldBe(Enumerable.Range(2, 100).Reverse().Select(TestId));
        first.Runs[0].ScheduledFor.ShouldBe(new DateTimeOffset(2026, 1, 1, 23, 59, 0, TimeSpan.Zero));
        first.Next.ShouldNotBeNull();
        second.Runs.ShouldHaveSingleItem().RunId.ShouldBe(TestId(1));
        second.Next.ShouldBeNull();
    }

    [Fact]
    public async Task Pages_fresh_active_observations_behind_newer_retained_history()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await CreateTable(DataSource, ct);
        await using var seed = DataSource.CreateCommand(
            """
            INSERT INTO "Scheduling ops"."Run history"
                (job_name, run_id, started_at, observed_until, finished_at, outcome)
            SELECT 'retention', ('00000000-0000-0000-0000-' || lpad(value::text, 12, '0'))::uuid,
                pg_catalog.statement_timestamp() - interval '20 minutes',
                pg_catalog.statement_timestamp() + interval '1 hour',
                pg_catalog.statement_timestamp() - interval '1 minute', 6
            FROM generate_series(10, 130) AS batch(value);

            INSERT INTO "Scheduling ops"."Run history" (job_name, run_id, started_at, observed_until)
            SELECT 'retention', ('00000000-0000-0000-0000-' || lpad(value::text, 12, '0'))::uuid,
                pg_catalog.statement_timestamp() - interval '1 hour',
                pg_catalog.statement_timestamp() + interval '1 hour'
            FROM generate_series(1, 3) AS batch(value);

            INSERT INTO "Scheduling ops"."Run history" (job_name, run_id, started_at, observed_until)
            VALUES
                ('retention', '00000000-0000-0000-0000-000000000004',
                    pg_catalog.statement_timestamp() - interval '10 minutes', pg_catalog.statement_timestamp()),
                ('Retention', '00000000-0000-0000-0000-000000000005',
                    pg_catalog.statement_timestamp() - interval '10 minutes',
                    pg_catalog.statement_timestamp() + interval '1 hour');
            """);
        _ = await seed.ExecuteNonQueryAsync(ct);
        var store = new PostgreSqlScheduleRunStore(DataSource, Schema, Table);

        // Act
        var unrestricted = await store.ReadPage(Job, null, 100, ct);
        var first = await store.ReadActivePage(Job, null, 2, ct);
        var second = await store.ReadActivePage(Job, first.Next, 2, ct);
        var differentJob = await store.ReadActivePage("Retention", null, 2, ct);

        // Assert
        unrestricted.Runs.Count.ShouldBe(100);
        unrestricted.Runs.All(run => run.Status != PostgreSqlScheduleRunStatus.Running).ShouldBeTrue();
        first.Runs.Select(run => (run.RunId, run.Status)).ShouldBe(
        [
            (TestId(3), PostgreSqlScheduleRunStatus.Running),
            (TestId(2), PostgreSqlScheduleRunStatus.Running),
        ]);
        first.Next.ShouldBe(new PostgreSqlScheduleRunCursor(first.Runs[^1].StartedAt, TestId(2)));
        var last = second.Runs.ShouldHaveSingleItem();
        last.RunId.ShouldBe(TestId(1));
        last.Status.ShouldBe(PostgreSqlScheduleRunStatus.Running);
        second.Next.ShouldBeNull();
        differentJob.Runs.ShouldHaveSingleItem().RunId.ShouldBe(TestId(5));
    }

    [Fact]
    public async Task Only_explicit_results_mark_retry_or_terminal_state_and_completion_is_monotonic()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await CreateTable(DataSource, ct);
        var store = new PostgreSqlScheduleRunStore(DataSource, Schema, Table);
        var failed = Guid.NewGuid();
        var retrying = Guid.NewGuid();
        var retryAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.FromHours(1));
        var scheduledFor = retryAt.AddDays(-1);
        var cancelled = Guid.NewGuid();
        var successful = Guid.NewGuid();
        _ = await store.Start(Job, failed, scheduledFor, ObservationWindow, ct);
        _ = await store.Start(Job, retrying, null, ObservationWindow, ct);
        _ = await store.Start(Job, cancelled, null, ObservationWindow, ct);
        _ = await store.Start(Job, successful, null, ObservationWindow, ct);

        // Act
        _ = await store.Complete(Job, failed, PostgreSqlScheduleRunStatus.Failed, null, ct);
        _ = await store.Complete(Job, retrying, PostgreSqlScheduleRunStatus.RetryScheduled, retryAt, ct);
        _ = await store.Complete(Job, cancelled, PostgreSqlScheduleRunStatus.Cancelled, null, ct);
        _ = await store.Complete(Job, successful, PostgreSqlScheduleRunStatus.Succeeded, null, ct);
        var lateCompletion = await store.Complete(Job, retrying, PostgreSqlScheduleRunStatus.TerminalFailure, null, ct);
        var lateObservation = await store.Refresh(Job, retrying, ObservationWindow, ct);
        var page = await store.ReadPage(Job, null, 100, ct);

        // Assert
        lateCompletion.ShouldBeFalse();
        lateObservation.ShouldBeFalse();
        page.Runs.Select(row => row.Status).ShouldBe(
        [
            PostgreSqlScheduleRunStatus.Succeeded,
            PostgreSqlScheduleRunStatus.Cancelled,
            PostgreSqlScheduleRunStatus.RetryScheduled,
            PostgreSqlScheduleRunStatus.Failed,
        ]);
        page.Runs.Where(row => row.RunId == retrying).ShouldHaveSingleItem().RetryAt.ShouldBe(retryAt.ToUniversalTime());
        var failedRun = page.Runs.Where(row => row.RunId == failed).ShouldHaveSingleItem();
        failedRun.RetryAt.ShouldBeNull();
        failedRun.ScheduledFor.ShouldBe(scheduledFor.ToUniversalTime());
        page.Runs.All(row => row.FinishedAt >= row.StartedAt).ShouldBeTrue();
    }

    [Fact]
    public async Task Repeated_start_and_competing_completions_keep_one_observation_in_the_original_job()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await CreateTable(DataSource, ct);
        await using var secondHost = NpgsqlDataSource.Create(ConnectionString);
        var first = new PostgreSqlScheduleRunStore(DataSource, Schema, Table);
        var second = new PostgreSqlScheduleRunStore(secondHost, Schema, Table);
        var run = Guid.NewGuid();
        var inserted = await first.Start(Job, run, null, ObservationWindow, ct);

        // Act
        var repeated = await second.Start(Job, run, null, ObservationWindow, ct);
        var otherScope = await second.Refresh("Retention", run, ObservationWindow, ct);
        var renewed = await first.Refresh(Job, run, ObservationWindow, ct);
        var completions = await Task.WhenAll(
            first.Complete(Job, run, PostgreSqlScheduleRunStatus.Cancelled, null, ct),
            second.Complete(Job, run, PostgreSqlScheduleRunStatus.TerminalFailure, null, ct));
        var page = await second.ReadPage(Job, null, 1, ct);

        // Assert
        inserted.ShouldBeTrue();
        repeated.ShouldBeFalse();
        otherScope.ShouldBeFalse();
        renewed.ShouldBeTrue();
        completions.Count(completed => completed).ShouldBe(1);
        page.Runs.ShouldHaveSingleItem().Status.ShouldBe(
            completions[0] ? PostgreSqlScheduleRunStatus.Cancelled : PostgreSqlScheduleRunStatus.TerminalFailure);
        page.Next.ShouldBeNull();
    }

    [Fact]
    public async Task A_canceled_read_and_completion_observe_the_caller_token()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var store = new PostgreSqlScheduleRunStore(DataSource, Schema, Table);

        // Act
        Func<Task> read = () => store.ReadPage(Job, null, 100, cancellation.Token);
        Func<Task> complete = () => store.Complete(Job, Guid.NewGuid(), PostgreSqlScheduleRunStatus.Cancelled, null, cancellation.Token);
        var readException = await read.ShouldThrowAssignableTo<OperationCanceledException>();
        var completionException = await complete.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        readException.CancellationToken.ShouldBe(cancellation.Token);
        completionException.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Rejects_page_sizes_outside_the_bounded_range(int size)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var store = new PostgreSqlScheduleRunStore(DataSource, Schema, Table);

        // Act
        Func<Task> read = () => store.ReadPage(Job, null, size, ct);
        var exception = await read.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        exception.ParamName.ShouldBe("pageSize");
    }

    [Theory]
    [InlineData(PostgreSqlScheduleRunStatus.Running, false, "outcome")]
    [InlineData(PostgreSqlScheduleRunStatus.RetryScheduled, false, "retryAt")]
    [InlineData(PostgreSqlScheduleRunStatus.Succeeded, true, "retryAt")]
    public async Task Rejects_completion_values_that_do_not_support_the_reported_state(
        PostgreSqlScheduleRunStatus outcome, bool supplyRetryTime, string parameter)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var store = new PostgreSqlScheduleRunStore(DataSource, Schema, Table);
        var retryAt = supplyRetryTime ? DateTimeOffset.UnixEpoch : (DateTimeOffset?)null;

        // Act
        Func<Task> complete = () => store.Complete(Job, Guid.NewGuid(), outcome, retryAt, ct);
        var exception = await complete.ShouldThrowAssignableTo<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public async Task Rejects_observation_windows_outside_the_freshness_bounds(int minutes)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var store = new PostgreSqlScheduleRunStore(DataSource, Schema, Table);

        // Act
        Func<Task> start = () => store.Start(Job, Guid.NewGuid(), null, TimeSpan.FromMinutes(minutes), ct);
        var exception = await start.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        exception.ParamName.ShouldBe("observationWindow");
    }

    [Fact]
    public void Rejects_a_relation_name_containing_nul()
    {
        // Arrange
        const string invalid = "invalid\0name";

        // Act
        Action create = () => _ = new PostgreSqlScheduleRunStore(DataSource, Schema, invalid);
        var exception = create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("identifier");
    }

    [Theory]
    [InlineData("invalid\0name", 0)]
    [InlineData("job", 129)]
    public async Task Rejects_job_names_that_exceed_the_metadata_contract(string name, int suffixLength)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var store = new PostgreSqlScheduleRunStore(DataSource, Schema, Table);
        var jobName = name + new string('x', suffixLength);

        // Act
        Func<Task> read = () => store.ReadPage(jobName, null, 1, ct);
        var exception = await read.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("jobName");
    }

    [Fact]
    public async Task Rejects_an_empty_run_identifier_before_writing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var store = new PostgreSqlScheduleRunStore(DataSource, Schema, Table);

        // Act
        Func<Task> start = () => store.Start(Job, Guid.Empty, null, ObservationWindow, ct);
        var exception = await start.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("runId");
    }
}
