using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A hand-written migration that creates the ledger table.</summary>
[DbContext(typeof(LedgerDbContext))]
[Migration(MigrationId)]
public sealed class CreateLedger : Migration
{
    /// <summary>The migration id.</summary>
    public const string MigrationId = "20260101000000_CreateLedger";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "Entries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Entries", entry => entry.Id));
    }
}
