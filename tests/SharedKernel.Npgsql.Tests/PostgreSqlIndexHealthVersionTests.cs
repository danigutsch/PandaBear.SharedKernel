using SharedKernel.IntegrationTesting;
using SharedKernel.Observability.Npgsql;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Verifies the collector's supported range against live PostgreSQL servers.</summary>
/// <param name="fixture">The shared server or a digest-pinned server supplied by the compatibility test run.</param>
public sealed class PostgreSqlIndexHealthVersionTests(PostgreSqlTestServerFixture fixture)
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("15.20", "7957245f5540d6bc56b0a65dc4c4e476b76782dc3e28b12309b276b6a9dd6996", 15)]
    [InlineData("19.0", "d2c8a7d3d224a9c0335bf9f69ae6ad0b9aa7d88c4b64bab89f0f261332f6d4de", 19)]
    public async Task Collection_respects_the_qualified_server_range_and_keeps_other_queries_available(
        string? tag,
        string? digest,
        int? expectedMajor)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        AspireTestApplication? ownedServer = null;
        if (tag is not null && digest is not null)
        {
            var builder = AspireTestApplication.CreateBuilder();
            _ = PostgreSqlTestServer.Add(builder).WithImageTag(tag).WithImageSHA256(digest);
            ownedServer = await AspireTestApplication.Start(
                builder, [PostgreSqlTestServer.ResourceName], resourceStartupTimeout: null, ct);
        }

        await using var app = ownedServer;
        var connectionString = app is null
            ? await fixture.GetServerConnectionString(ct)
            : await app.GetConnectionString(PostgreSqlTestServer.ResourceName, ct);
        await using var pool = new PostgreSqlTestDatabasePool(connectionString);
        await using var database = await pool.Create(ct);
        await using var connection = await database.DataSource.OpenConnectionAsync(ct);
        var serverMajor = connection.PostgreSqlVersion.Major;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE public.index_health_version_probe (id integer PRIMARY KEY);
            INSERT INTO public.index_health_version_probe VALUES (1);
            """;
        await command.ExecuteNonQueryAsync(ct);
        var collector = new PostgreSqlIndexHealthCollector(database.DataSource);

        // Act
        var collection = await collector.Collect(ct);
        command.CommandText = "SELECT count(*) FROM public.index_health_version_probe;";
        var rowCount = await command.ExecuteScalarAsync(ct);

        // Assert
        if (expectedMajor is not null)
        {
            serverMajor.ShouldBe(expectedMajor.Value);
        }

        var supported = serverMajor is >= 16 and <= 18;
        collection.Outcome.ShouldBe(supported
            ? PostgreSqlIndexHealthCollectionOutcome.Collected
            : PostgreSqlIndexHealthCollectionOutcome.Unsupported);
        if (supported)
        {
            collection.Assessments.ShouldContain(item => item.Evidence.TableName == "index_health_version_probe");
        }
        else
        {
            collection.Assessments.ShouldBeEmpty();
        }

        rowCount.ShouldBe(1L);
    }
}
