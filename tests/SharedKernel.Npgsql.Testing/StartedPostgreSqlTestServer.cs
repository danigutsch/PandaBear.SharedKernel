namespace PandaBear.Npgsql.Testing;

/// <summary>A PostgreSQL test server that an Aspire application started, together with the way to stop it.</summary>
/// <param name="connectionString">The connection string of the server, without a database.</param>
/// <param name="stop">Stops the server by disposing the application that owns its container.</param>
internal sealed class StartedPostgreSqlTestServer(string connectionString, Func<ValueTask> stop) : IAsyncDisposable
{
    /// <summary>Gets the connection string of the server, without a database.</summary>
    public string ConnectionString
    {
        get;
    } = connectionString;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => stop();
}
