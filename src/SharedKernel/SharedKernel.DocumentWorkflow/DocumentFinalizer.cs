using SharedKernel.DocumentRendering;
using SharedKernel.Results;
using SharedKernel.Review;

namespace SharedKernel.DocumentWorkflow;

/// <summary>Renders and stores an artifact for an approved revision through host-supplied ports.</summary>
/// <typeparam name="TSource">The application's source model.</typeparam>
public sealed class DocumentFinalizer<TSource>
    where TSource : notnull
{
    private readonly DocumentGenerator<TSource> generator;
    private readonly IDocumentRenderer renderer;
    private readonly IDocumentArtifactStore store;
    private readonly string contentType;
    private readonly int maximumArtifactBytes;

    /// <summary>Creates a finalizer with an explicit artifact size and format policy.</summary>
    /// <param name="generator">The source and template used to check current provenance.</param>
    /// <param name="renderer">The deterministic renderer for the selected format.</param>
    /// <param name="store">The immutable artifact store.</param>
    /// <param name="contentType">The host-configured media type of the renderer output.</param>
    /// <param name="maximumArtifactBytes">The maximum accepted output length.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the media type is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the output limit is not positive.</exception>
    public DocumentFinalizer(
        DocumentGenerator<TSource> generator, IDocumentRenderer renderer, IDocumentArtifactStore store,
        string contentType, int maximumArtifactBytes)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumArtifactBytes);
        this.generator = generator;
        this.renderer = renderer;
        this.store = store;
        this.contentType = contentType;
        this.maximumArtifactBytes = maximumArtifactBytes;
    }

    /// <summary>Seals a revision after durably storing the exact approved renderer output.</summary>
    /// <param name="revision">The current document snapshot.</param>
    /// <param name="reviewed">The approval version identified by the command.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The sealed snapshot to persist, or a failure leaving the revision unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the revision is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <remarks>The host persists the resulting snapshot with a concurrency guard. Superseded attempts may leave unreferenced artifacts for host cleanup.</remarks>
    public async ValueTask<Result<DocumentRevision>> FinalizeRevision(
        DocumentRevision revision, ReviewRevision reviewed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ct.ThrowIfCancellationRequested();
        var approval = revision.CheckApproval(reviewed);
        if (approval.IsFailure)
        {
            return approval.ConvertError<DocumentRevision>();
        }

        var origin = await generator.ReadCurrentOrigin(revision.Origin.SourceId, ct).ConfigureAwait(false);
        if (origin.IsFailure)
        {
            return origin.ConvertError<DocumentOrigin, DocumentRevision>();
        }

        var freshness = revision.CheckOrigin(origin.Value);
        if (freshness.IsFailure)
        {
            return freshness.ConvertError<DocumentRevision>();
        }

        ct.ThrowIfCancellationRequested();
        var content = renderer.Render(revision.Content.ToRenderRequest());
        ct.ThrowIfCancellationRequested();
        if (content.Length == 0 || content.Length > maximumArtifactBytes)
        {
            return Result.Invalid<DocumentRevision>("The rendered artifact is outside the permitted size.",
                "artifact", "Supply a non-empty artifact within the configured size limit.");
        }

        var artifact = new DocumentArtifactWrite(revision, contentType, content);
        var stored = await store.Write(artifact, ct).ConfigureAwait(false);
        if (stored.IsFailure)
        {
            return stored.ConvertError<string, DocumentRevision>();
        }

        if (string.IsNullOrWhiteSpace(stored.Value))
        {
            return Result.Error<DocumentRevision>("The artifact store did not return a private handle.");
        }

        ct.ThrowIfCancellationRequested();
        var current = await generator.ReadCurrentOrigin(revision.Origin.SourceId, ct).ConfigureAwait(false);
        return current.Bind(snapshot => revision.Seal(stored.Value, reviewed, snapshot));
    }
}
