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
    /// Gets or sets the schema containing the integration-event tables.
    /// </summary>
    public string Schema { get; set; } = SharedKernelSchemas.Messaging;

    /// <summary>
    /// Gets or sets the outbox table name.
    /// </summary>
    public string OutboxTableName { get; set; } = DefaultOutboxTableName;

    /// <summary>
    /// Gets or sets the transport table name.
    /// </summary>
    public string TransportTableName { get; set; } = DefaultTransportTableName;
}
