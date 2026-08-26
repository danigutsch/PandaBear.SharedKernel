using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Idempotency.EntityFrameworkCore;

/// <summary>
/// Configures the relational storage owned by one idempotency DbContext.
/// </summary>
public sealed class IdempotencyStorageOptions
{
    internal const string DefaultTableName = "idempotency_keys";

    /// <summary>
    /// Gets or sets the schema containing the idempotency table.
    /// </summary>
    public string Schema { get; set; } = SharedKernelSchemas.Messaging;

    /// <summary>
    /// Gets or sets the idempotency table name.
    /// </summary>
    public string TableName { get; set; } = DefaultTableName;
}
