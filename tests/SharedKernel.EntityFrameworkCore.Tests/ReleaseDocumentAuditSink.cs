using SharedKernel.AuditTrail;
using SharedKernel.DocumentWorkflow.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Appends the finalization audit to the same PostgreSQL transaction.</summary>
/// <param name="context">The owning context.</param>
/// <param name="failure">The optional test fault after flushing the audit row.</param>
internal sealed class ReleaseDocumentAuditSink(DocumentWorkflowTestContext context, ReleaseDocumentAuditFailure failure)
    : IAuditTrailSink<DocumentFinalizationAuditEntry>
{
    /// <inheritdoc />
    public async ValueTask Append(DocumentFinalizationAuditEntry entry, CancellationToken ct)
    {
        context.Set<DocumentFinalizationAuditEntry>().Add(entry);
        await context.SaveChangesAsync(ct);
        failure.Check();
    }
}
