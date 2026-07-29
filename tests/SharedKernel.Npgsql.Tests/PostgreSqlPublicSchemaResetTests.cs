using SharedKernel.IntegrationTesting;

namespace SharedKernel.Npgsql.Tests;

public sealed class PostgreSqlPublicSchemaResetTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    [Fact]
    public async Task Reset_truncates_public_tables_restarts_identities_and_preserves_excluded_and_migration_tables()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await DataSource.OpenConnectionAsync(ct);
        await using var setupCommand = new NpgsqlCommand(
            """
            CREATE TABLE public.reset_parent (
                id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY
            );

            CREATE TABLE public.reset_child (
                id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                parent_id integer NOT NULL REFERENCES public.reset_parent(id)
            );

            CREATE TABLE public.reset_peer (
                id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY
            );

            CREATE TABLE public.excluded_table (
                id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY
            );

            CREATE TABLE public."__EFMigrationsHistory" (
                id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY
            );

            INSERT INTO public.reset_parent DEFAULT VALUES;
            INSERT INTO public.reset_child (parent_id) VALUES (1);
            INSERT INTO public.reset_peer DEFAULT VALUES;
            INSERT INTO public.excluded_table DEFAULT VALUES;
            INSERT INTO public."__EFMigrationsHistory" DEFAULT VALUES;
            """,
            connection);
        _ = await setupCommand.ExecuteNonQueryAsync(ct);

        // Act
        await PostgreSqlPublicSchemaReset.Reset(connection, ["excluded_table"], ct);

        // Assert
        await using (var verificationCommand = new NpgsqlCommand(
            """
            SELECT
                (SELECT count(*) FROM public.reset_parent),
                (SELECT count(*) FROM public.reset_child),
                (SELECT count(*) FROM public.reset_peer),
                (SELECT count(*) FROM public.excluded_table),
                (SELECT count(*) FROM public."__EFMigrationsHistory");
            """,
            connection))
        await using (var reader = await verificationCommand.ExecuteReaderAsync(ct))
        {
            var hasRow = await reader.ReadAsync(ct);
            hasRow.ShouldBeTrue();
            reader.GetInt64(0).ShouldBe(0L);
            reader.GetInt64(1).ShouldBe(0L);
            reader.GetInt64(2).ShouldBe(0L);
            reader.GetInt64(3).ShouldBe(1L);
            reader.GetInt64(4).ShouldBe(1L);
        }

        await using (var identityCommand = new NpgsqlCommand(
            """
            WITH parent_row AS (
                INSERT INTO public.reset_parent DEFAULT VALUES RETURNING id
            ), child_row AS (
                INSERT INTO public.reset_child (parent_id)
                SELECT id FROM parent_row
                RETURNING id
            ), peer_row AS (
                INSERT INTO public.reset_peer DEFAULT VALUES RETURNING id
            ), excluded_row AS (
                INSERT INTO public.excluded_table DEFAULT VALUES RETURNING id
            )
            SELECT
                (SELECT id FROM parent_row),
                (SELECT id FROM child_row),
                (SELECT id FROM peer_row),
                (SELECT id FROM excluded_row);
            """,
            connection))
        await using (var reader = await identityCommand.ExecuteReaderAsync(ct))
        {
            var hasRow = await reader.ReadAsync(ct);
            hasRow.ShouldBeTrue();
            reader.GetInt32(0).ShouldBe(1);
            reader.GetInt32(1).ShouldBe(1);
            reader.GetInt32(2).ShouldBe(1);
            reader.GetInt32(3).ShouldBe(2);
        }
    }

    [Fact]
    public async Task Fails_atomically_when_an_excluded_child_references_a_reset_parent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await DataSource.OpenConnectionAsync(ct);
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
