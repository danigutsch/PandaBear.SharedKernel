namespace PandaBear.EventSourcing.Npgsql;

/// <summary>
/// Configures PostgreSQL event-sourcing storage.
/// </summary>
public sealed class PostgreSqlEventSourcingOptions
{
    /// <summary>Gets or sets the clock used for persisted append and checkpoint metadata.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets or sets the PostgreSQL schema used by the provider.
    /// </summary>
    public string Schema { get; set; } = "event_sourcing";
}
