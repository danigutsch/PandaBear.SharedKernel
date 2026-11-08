namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A row of the migration test context.</summary>
public sealed class LedgerEntry
{
    /// <summary>Gets or sets the key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
}
