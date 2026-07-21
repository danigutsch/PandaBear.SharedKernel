using SharedKernel.IntegrationTesting;

namespace SharedKernel.Npgsql.Tests;

internal sealed class PostgreSqlTransactionAdvisoryLockTestEnvironment : IAsyncDisposable
{
    private const string PostgreSqlResourceName = "postgres";
    private const string DatabaseResourceName = "sharedkernelnpgsql";

    private readonly AspireTestApplication _app;

    private PostgreSqlTransactionAdvisoryLockTestEnvironment(AspireTestApplication app, NpgsqlDataSource dataSource)
    {
        _app = app;
        DataSource = dataSource;
    }

    public NpgsqlDataSource DataSource { get; }

    public static async Task<PostgreSqlTransactionAdvisoryLockTestEnvironment> Start(CancellationToken ct)
    {
        var appBuilder = AspireTestApplication.CreateBuilder();
        var databaseServer = appBuilder.AddPostgres(PostgreSqlResourceName);
        _ = databaseServer.AddDatabase(DatabaseResourceName);

        var app = await AspireTestApplication.Start(appBuilder, [PostgreSqlResourceName], null, ct);
        try
        {
            var connectionString = await app.GetConnectionString(DatabaseResourceName, ct);
            return new PostgreSqlTransactionAdvisoryLockTestEnvironment(app, NpgsqlDataSource.Create(connectionString));
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _app.DisposeAsync();
    }
}
