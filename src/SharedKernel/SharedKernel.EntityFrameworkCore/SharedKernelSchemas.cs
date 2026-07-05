namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Defines shared database schema names used by reusable persistence providers.
/// </summary>
public static class SharedKernelSchemas
{
    /// <summary>
    /// The schema for durable messaging and idempotency tables.
    /// </summary>
    public const string Messaging = "messaging";
}
