using Microsoft.EntityFrameworkCore;
using PandaBear.DocumentWorkflow.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Owns the neutral document workflow schema used by tests.</summary>
/// <param name="options">The test database options.</param>
/// <param name="configurations">The registered workflow model configurations.</param>
internal sealed class DocumentWorkflowTestContext(
    DbContextOptions<DocumentWorkflowTestContext> options,
    IEnumerable<IDbContextConfiguration<DocumentWorkflowTestContext>> configurations) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var configuration in configurations)
        {
            configuration.ConfigureModel(modelBuilder);
        }

        modelBuilder.Entity<DocumentFinalizationAuditEntry>().HasKey(entry => new { entry.LineageId, entry.RevisionNumber });
    }
}
