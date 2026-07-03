namespace SharedKernel.EventSourcing.Npgsql;

/// <summary>
/// Configures PostgreSQL event-sourcing storage.
/// </summary>
public sealed class PostgreSqlEventSourcingOptions
{
    /// <summary>
    /// Gets or sets the PostgreSQL schema used by the provider.
    /// </summary>
    public string Schema { get; set; } = "event_sourcing";
}
