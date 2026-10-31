namespace PandaBear.Observability.Npgsql;

/// <summary>Configures reusable, read-only PostgreSQL index-health monitoring.</summary>
public sealed class PostgreSqlIndexHealthMonitoringOptions
{
    /// <summary>Gets or sets the interval between completed collection cycles.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Gets or sets the maximum duration of one read-only PostgreSQL command.</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Throws when the polling interval or command timeout is outside its supported range.</summary>
    /// <exception cref="InvalidOperationException">When <see cref="PollingInterval"/> or <see cref="CommandTimeout"/> is outside its supported range.</exception>
    internal void Validate()
    {
        if (PollingInterval < TimeSpan.FromMinutes(1)
            || PollingInterval > TimeSpan.FromDays(1)
            || CommandTimeout <= TimeSpan.Zero
            || CommandTimeout > TimeSpan.FromMinutes(5))
        {
            throw new InvalidOperationException("PostgreSQL index-health monitoring has an invalid polling interval or command timeout.");
        }
    }
}
