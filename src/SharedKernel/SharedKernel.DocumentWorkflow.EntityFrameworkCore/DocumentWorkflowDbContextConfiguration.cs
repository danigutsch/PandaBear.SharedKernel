using Microsoft.EntityFrameworkCore;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Maps the versioned workflow rows in the owning context.</summary>
/// <typeparam name="TContext">The context that owns the table.</typeparam>
internal sealed class DocumentWorkflowDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    /// <summary>The rule requiring a unique revision number in each lineage.</summary>
    internal const string RevisionIdentityRule = "document-workflow.revision";
    private const string Schema = "document_workflow";
    private const string Table = "revisions";

    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<DocumentWorkflowRow>(row =>
        {
            row.ToTable(Table, Schema);
            row.HasKey(revision => revision.Id);
            row.HasIndex(revision => new { revision.LineageId, revision.Number }).HasUniqueRule(RevisionIdentityRule);
            row.Property(revision => revision.Version).IsConcurrencyToken();
        });
    }
}
