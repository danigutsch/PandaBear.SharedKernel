namespace PandaBear.Npgsql.Tests;

/// <summary>Stands in for the process environment, so a test never changes a process-wide variable.</summary>
internal static class FakeEnvironment
{
    /// <summary>Gets an environment in which the borrowed-server variable is not set.</summary>
    public static Func<string, string?> WithoutServer => WithServer(null);

    /// <summary>
    /// Creates an environment in which only the borrowed-server variable is set, to <paramref name="value"/>.
    /// Reading any other variable fails, so a test notices code that reads more than the contract names.
    /// </summary>
    public static Func<string, string?> WithServer(string? value)
    {
        return name => string.Equals(name, PostgreSqlTestServer.ConnectionStringVariable, StringComparison.Ordinal)
            ? value
            : throw new InvalidOperationException($"The test environment defines no variable '{name}'.");
    }

    /// <summary>
    /// Creates an environment in which only the owner host's ready-file variable is set, to <paramref name="value"/>.
    /// Reading any other variable fails, so a test notices code that reads more than the contract names.
    /// </summary>
    public static Func<string, string?> WithReadyFile(string? value)
    {
        return name => string.Equals(name, PostgreSqlTestServer.ReadyFileVariable, StringComparison.Ordinal)
            ? value
            : throw new InvalidOperationException($"The test environment defines no variable '{name}'.");
    }
}
