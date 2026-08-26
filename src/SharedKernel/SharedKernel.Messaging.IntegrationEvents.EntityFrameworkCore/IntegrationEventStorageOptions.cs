using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Configures the relational storage owned by one integration-event DbContext.
/// </summary>
public sealed class IntegrationEventStorageOptions
{
    internal const string DefaultOutboxTableName = "outbox_messages";
    internal const string DefaultTransportTableName = "transport_messages";

    /// <summary>
    /// Gets or sets the default schema used when a table-specific schema is not configured.
    /// </summary>
    public string Schema { get; set; } = SharedKernelSchemas.Messaging;

    /// <summary>
    /// Gets or sets the optional outbox schema override. When null, <see cref="Schema" /> is used.
    /// </summary>
    public string? OutboxSchema { get; set; }

    /// <summary>
    /// Gets or sets the outbox table name.
    /// </summary>
    public string OutboxTableName { get; set; } = DefaultOutboxTableName;

    /// <summary>
    /// Gets or sets the optional transport schema override. When null, <see cref="Schema" /> is used.
    /// </summary>
    public string? TransportSchema { get; set; }

    /// <summary>
    /// Gets or sets the transport table name.
    /// </summary>
    public string TransportTableName { get; set; } = DefaultTransportTableName;

    /// <summary>
    /// Gets or sets a value indicating whether the transport table is mapped for runtime access but excluded from this context's migrations.
    /// </summary>
    public bool ExcludeTransportFromMigrations { get; set; }

    internal string EffectiveOutboxSchema => OutboxSchema ?? Schema;

    internal string EffectiveTransportSchema => TransportSchema ?? Schema;
}
