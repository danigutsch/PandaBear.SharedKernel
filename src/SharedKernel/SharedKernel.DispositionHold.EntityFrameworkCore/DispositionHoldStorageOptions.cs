namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>
/// Configures the relational storage owned by one disposition hold DbContext.
/// </summary>
public sealed class DispositionHoldStorageOptions
{
    /// <summary>Schema used when the options do not set one.</summary>
    internal const string DefaultSchema = "disposition_hold";

    /// <summary>Table name used when the options do not set one.</summary>
    internal const string DefaultTableName = "holds";

    /// <summary>Gets or sets the schema containing the hold table.</summary>
    public string Schema { get; set; } = DefaultSchema;

    /// <summary>Gets or sets the hold table name.</summary>
    public string TableName { get; set; } = DefaultTableName;
}
