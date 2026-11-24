using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PandaBear.AuditTrail;
using PandaBear.Idempotency;
using PandaBear.Results;

namespace PandaBear.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Finalizes a revision and commits its audit and idempotency state in the owning EF transaction.</summary>
/// <typeparam name="TContext">The context that owns the document and idempotency state.</typeparam>
/// <typeparam name="TSource">The host's document source model.</typeparam>
/// <param name="context">The context bound to the mediator transaction.</param>
/// <param name="documents">The document store for the same context.</param>
/// <param name="finalizer">The host-configured artifact finalizer.</param>
/// <param name="idempotency">The idempotency store bound to the same context.</param>
/// <param name="auditTrail">The audit sink participating in the same transaction.</param>
/// <param name="clock">The clock for audit and idempotency timestamps.</param>
/// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
public sealed class FinalizeDocumentRevisionHandler<TContext, TSource>(
    TContext context,
    DocumentWorkflowStore<TContext> documents,
    DocumentFinalizer<TSource> finalizer,
    IIdempotencyStore idempotency,
    IAuditTrailSink<DocumentFinalizationAuditEntry> auditTrail,
    TimeProvider clock)
    where TContext : DbContext
    where TSource : notnull
{
    private static readonly IdempotencyScope Scope = IdempotencyScope.From("document-workflow.finalize.v1");
    private readonly TContext dbContext = context ?? throw new ArgumentNullException(nameof(context));
    private readonly DocumentWorkflowStore<TContext> store = documents ?? throw new ArgumentNullException(nameof(documents));
    private readonly DocumentFinalizer<TSource> artifactFinalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
    private readonly IIdempotencyStore idempotencyStore = idempotency ?? throw new ArgumentNullException(nameof(idempotency));
    private readonly IAuditTrailSink<DocumentFinalizationAuditEntry> auditSink = auditTrail ?? throw new ArgumentNullException(nameof(auditTrail));
    private readonly TimeProvider timeProvider = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>Processes an authorized finalization request or replays its committed receipt.</summary>
    /// <param name="request">The authorized command.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The committed finalization information or a typed failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active EF transaction.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    public async ValueTask<Result<DocumentFinalizationReceipt>> Handle(DocumentFinalizationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Document finalization requires an active EF transaction.");
        }

        if (request.LineageId == Guid.Empty || request.RevisionNumber <= 0 || request.ExpectedVersion is <= 0 or long.MaxValue ||
            request.Reviewed is null || string.IsNullOrWhiteSpace(request.ActorPseudonym) || request.IdempotencyKey == default)
        {
            return Result.Invalid<DocumentFinalizationReceipt>(
                "The finalization command is incomplete.", "command", "Supply the revision, approval, operator, and idempotency key.");
        }

        var operation = new IdempotencyOperation(Scope, IdempotencyKey.From(Hash(request.IdempotencyKey.Value)));
        var owner = request.LineageId.ToString("N");
        var fingerprint = Hash(FormattableString.Invariant(
            $"{owner}|{request.RevisionNumber}|{request.ExpectedVersion}|{request.Reviewed.Number}|{request.Reviewed.ContentHash}|{request.ActorPseudonym}"));
        var started = await idempotencyStore.TryStart(operation, timeProvider.GetUtcNow(), lockDuration: null, retentionOwner: owner, ct)
            .ConfigureAwait(false);
        if (!started.Started)
        {
            return await Replay(request, started.ExistingEntry, fingerprint, ct).ConfigureAwait(false);
        }

        var loaded = await store.Load(request.LineageId, request.RevisionNumber, ct).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.ConvertError<PersistedDocumentRevision, DocumentFinalizationReceipt>();
        }

        var revision = loaded.Value;
        if (revision.Version != request.ExpectedVersion)
        {
            return Result<DocumentFinalizationReceipt>.FromError(
                new ResultError("The document revision changed. Reload it and try again.", ResultErrorCodes.ConcurrencyConflict));
        }

        var finalized = await artifactFinalizer.FinalizeRevision(revision.Revision, request.Reviewed, ct).ConfigureAwait(false);
        if (finalized.IsFailure)
        {
            return finalized.ConvertError<DocumentRevision, DocumentFinalizationReceipt>();
        }

        var saved = await store.Save(finalized.Value, request.ExpectedVersion, ct).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return saved.ConvertError<PersistedDocumentRevision, DocumentFinalizationReceipt>();
        }

        var completedAt = timeProvider.GetUtcNow();
        var receipt = CreateReceipt(request, finalized.Value.ArtifactReference!);
        await auditSink.Append(new DocumentFinalizationAuditEntry(
            receipt.LineageId, receipt.RevisionNumber, receipt.Version, request.ActorPseudonym, completedAt.UtcDateTime), ct)
            .ConfigureAwait(false);
        await idempotencyStore.Complete(operation, completedAt, fingerprint, ct).ConfigureAwait(false);
        return Result.Ok(receipt);
    }

    private async ValueTask<Result<DocumentFinalizationReceipt>> Replay(
        DocumentFinalizationRequest request, IdempotencyEntry? entry, string fingerprint, CancellationToken ct)
    {
        if (entry is not { State: IdempotencyEntryState.Completed } ||
            !string.Equals(entry.ResultFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return Result.Conflict<DocumentFinalizationReceipt>("The idempotency key belongs to another or unfinished request.");
        }

        var loaded = await store.Load(request.LineageId, request.RevisionNumber, ct).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.ConvertError<PersistedDocumentRevision, DocumentFinalizationReceipt>();
        }

        var stored = loaded.Value;
        return stored.Version > request.ExpectedVersion &&
            stored.Revision.Review.Revision == request.Reviewed &&
            stored.Revision.ArtifactReference is { } artifact
            ? Result.Ok(CreateReceipt(request, artifact))
            : Result.Conflict<DocumentFinalizationReceipt>("The committed artifact could not be recovered.");
    }

    private static DocumentFinalizationReceipt CreateReceipt(DocumentFinalizationRequest request, string artifact) =>
        new(request.LineageId, request.RevisionNumber, request.ExpectedVersion + 1, artifact);

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
