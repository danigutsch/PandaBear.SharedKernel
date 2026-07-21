namespace SharedKernel.Npgsql.Tests;

public sealed class PostgreSqlPublicSchemaResetTests
{
    [Fact]
    public async Task Fails_atomically_when_an_excluded_child_references_a_reset_parent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var environment = await PostgreSqlTransactionAdvisoryLockTestEnvironment.Start(ct);
        await using var connection = await environment.DataSource.OpenConnectionAsync(ct);
        await using var setupCommand = new NpgsqlCommand(
            """
            CREATE TABLE public.reset_parent (
                id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY
            );

            CREATE TABLE public.excluded_child (
                id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                parent_id integer NOT NULL REFERENCES public.reset_parent(id)
            );

            CREATE TABLE public.reset_peer (
                id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY
            );

            INSERT INTO public.reset_parent DEFAULT VALUES;
            INSERT INTO public.excluded_child (parent_id) VALUES (1);
            INSERT INTO public.reset_peer DEFAULT VALUES;
            """,
            connection);
        _ = await setupCommand.ExecuteNonQueryAsync(ct);

        // Act
        var exception = await PostgreSqlPublicSchemaResetTestHelpers.CaptureExcludedChildResetFailure(connection, ct);

        // Assert
        exception.ShouldNotBeNull().SqlState.ShouldBe(PostgresErrorCodes.FeatureNotSupported);
        await using var verificationCommand = new NpgsqlCommand(
            """
            SELECT
                (SELECT count(*) FROM public.reset_parent),
                (SELECT count(*) FROM public.excluded_child),
                (SELECT count(*) FROM public.reset_peer),
                (SELECT last_value FROM public.reset_peer_id_seq),
                (SELECT is_called FROM public.reset_peer_id_seq);
            """,
            connection);
        await using var reader = await verificationCommand.ExecuteReaderAsync(ct);
        var hasRow = await reader.ReadAsync(ct);
        hasRow.ShouldBeTrue();
        reader.GetInt64(0).ShouldBe(1L);
        reader.GetInt64(1).ShouldBe(1L);
        reader.GetInt64(2).ShouldBe(1L);
        reader.GetInt64(3).ShouldBe(1L);
        reader.GetBoolean(4).ShouldBeTrue();
    }
}
