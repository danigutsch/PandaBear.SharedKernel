using Microsoft.EntityFrameworkCore;
using PandaBear.BuildingBlocks;
using PandaBear.EntityFrameworkCore;
using PandaBear.Results;

namespace PandaBear.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Persists workflow revisions with an optimistic storage version.</summary>
/// <typeparam name="TContext">The context whose migrations own the document workflow table.</typeparam>
/// <param name="context">The EF unit of work.</param>
/// <param name="violationParser">The configured database provider's conflict parser.</param>
/// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
/// <remarks>Each write saves the owning context. Dispose the unit of work after a failed save before retrying.</remarks>
public sealed class DocumentWorkflowStore<TContext>(TContext context, IUniqueViolationParser violationParser)
    where TContext : DbContext
{
    private readonly TContext dbContext = context ?? throw new ArgumentNullException(nameof(context));
    private readonly IUniqueViolationParser parser = violationParser ?? throw new ArgumentNullException(nameof(violationParser));

    /// <summary>Loads and validates a current or historical revision.</summary>
    public async ValueTask<Result<PersistedDocumentRevision>> Load(Guid lineageId, int number, CancellationToken ct)
    {
        var row = await dbContext.Set<DocumentWorkflowRow>().AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.LineageId == lineageId && candidate.Number == number, ct)
            .ConfigureAwait(false);
        return row?.Restore() ?? Result.NotFound<PersistedDocumentRevision>("The document revision was not found.");
    }

    /// <summary>Inserts the initial generated draft in a lineage.</summary>
    /// <exception cref="ArgumentNullException">Thrown when the revision is null.</exception>
    public async ValueTask<Result<PersistedDocumentRevision>> Create(DocumentRevision revision, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.Number != 1 || revision.Status != DocumentLifecycleStatus.DraftGenerated)
        {
            return Invalid("A lineage must begin at its first generated draft.");
        }

        var row = new DocumentWorkflowRow(revision);
        dbContext.Set<DocumentWorkflowRow>().Add(row);
        return await Commit(revision, row, ct).ConfigureAwait(false);
    }

    /// <summary>Saves one revision with the version observed by the caller.</summary>
    /// <exception cref="ArgumentNullException">Thrown when the revision is null.</exception>
    public async ValueTask<Result<PersistedDocumentRevision>> Save(
        DocumentRevision revision, long expectedVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.Status == DocumentLifecycleStatus.Superseded)
        {
            return Invalid("Superseding a revision requires an atomic replacement.");
        }

        var matched = await MatchForWrite(revision, expectedVersion, ct).ConfigureAwait(false);
        if (matched.IsFailure)
        {
            return matched.ConvertError<DocumentWorkflowRow, PersistedDocumentRevision>();
        }

        var row = matched.Value;
        row.Write(revision);
        return await Commit(revision, row, ct).ConfigureAwait(false);
    }

    /// <summary>Saves the historical state and its fresh successor in one unit of work.</summary>
    /// <exception cref="ArgumentNullException">Thrown when a revision is null.</exception>
    public async ValueTask<Result<PersistedDocumentRevision>> Replace(
        DocumentRevision previous, DocumentRevision current, long expectedVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.LineageId != current.LineageId || (long)previous.Number + 1 != current.Number ||
            previous.Status != DocumentLifecycleStatus.Superseded || current.Status != DocumentLifecycleStatus.DraftGenerated ||
            previous.Origin.SourceId != current.Origin.SourceId)
        {
            return Invalid("Regeneration requires consecutive revisions from the same document source.");
        }

        var matched = await MatchForWrite(previous, expectedVersion, ct).ConfigureAwait(false);
        if (matched.IsFailure)
        {
            return matched.ConvertError<DocumentWorkflowRow, PersistedDocumentRevision>();
        }

        matched.Value.Write(previous);
        var next = new DocumentWorkflowRow(current);
        dbContext.Set<DocumentWorkflowRow>().Add(next);
        return await Commit(current, next, ct).ConfigureAwait(false);
    }

    private async ValueTask<Result<DocumentWorkflowRow>> MatchForWrite(
        DocumentRevision candidate, long expectedVersion, CancellationToken ct)
    {
        var row = await dbContext.Set<DocumentWorkflowRow>()
            .SingleOrDefaultAsync(item => item.LineageId == candidate.LineageId && item.Number == candidate.Number, ct)
            .ConfigureAwait(false);
        if (row is null || row.Version != expectedVersion)
        {
            return Stale<DocumentWorkflowRow>();
        }

        var stored = row.Restore();
        if (stored.IsFailure)
        {
            return stored.ConvertError<PersistedDocumentRevision, DocumentWorkflowRow>();
        }

        var previous = stored.Value.Revision;
        return previous.Status is DocumentLifecycleStatus.Superseded or DocumentLifecycleStatus.Voided ||
            previous.Origin != candidate.Origin ||
            (previous.Status != DocumentLifecycleStatus.DraftGenerated && candidate.Status == DocumentLifecycleStatus.DraftGenerated) ||
            !PreservesReviewHistory(previous, candidate) ||
            (previous.ArtifactReference is not null &&
                (!string.Equals(previous.ArtifactReference, candidate.ArtifactReference, StringComparison.Ordinal) ||
                 previous.Review.Revision != candidate.Review.Revision))
            ? Result.Invalid<DocumentWorkflowRow>("The write would alter immutable revision history.", "revision", "Keep the original provenance, evidence and artifact.")
            : Result.Ok(row);
    }

    private static bool PreservesReviewHistory(DocumentRevision previous, DocumentRevision candidate)
    {
        var before = previous.Review.Snapshot();
        var after = candidate.Review.Snapshot();
        return (after.Revision == before.Revision || after.Revision.Number > before.Revision.Number) &&
            before.Lanes.SequenceEqual(after.Lanes, StringComparer.Ordinal) &&
            after.Evidence.Take(before.Evidence.Length).SequenceEqual(before.Evidence);
    }

    private async ValueTask<Result<PersistedDocumentRevision>> Commit(
        DocumentRevision revision, DocumentWorkflowRow row, CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesTranslatingConflicts(parser, ct).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            return Stale<PersistedDocumentRevision>();
        }
        catch (UniqueConstraintViolationException exception)
            when (exception.Rule == DocumentWorkflowDbContextConfiguration<TContext>.RevisionIdentityRule)
        {
            return Stale<PersistedDocumentRevision>();
        }

        return Result.Ok(new PersistedDocumentRevision(revision, row.Version));
    }

    private static Result<T> Stale<T>() where T : notnull =>
        Result<T>.FromError(new ResultError("The document revision changed. Reload it and try again.", ResultErrorCodes.ConcurrencyConflict));

    private static Result<PersistedDocumentRevision> Invalid(string detail) =>
        Result.Invalid<PersistedDocumentRevision>(detail, "revision", "The revision is not valid for this operation.");
}
