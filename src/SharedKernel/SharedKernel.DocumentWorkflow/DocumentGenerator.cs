using PandaBear.Results;

namespace PandaBear.DocumentWorkflow;

/// <summary>Generates document revisions from host-provided source and template ports.</summary>
/// <typeparam name="TSource">The application-owned source model.</typeparam>
public sealed class DocumentGenerator<TSource>
    where TSource : notnull
{
    private readonly IDocumentSource<TSource> source;
    private readonly IDocumentTemplate<TSource> template;

    /// <summary>Creates a generator using the selected source and template.</summary>
    /// <param name="source">The source port.</param>
    /// <param name="template">The template port.</param>
    /// <exception cref="ArgumentNullException">Thrown when either port is null.</exception>
    public DocumentGenerator(IDocumentSource<TSource> source, IDocumentTemplate<TSource> template)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(template);
        this.source = source;
        this.template = template;
    }

    /// <summary>Generates the first draft in a lineage.</summary>
    /// <param name="lineageId">The identity of the document lineage.</param>
    /// <param name="sourceId">The application's source identity.</param>
    /// <param name="lanes">The required human review lanes in order.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The draft, or a source failure.</returns>
    /// <exception cref="ArgumentException">Thrown when either identifier is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the review lanes are null.</exception>
    public async ValueTask<Result<DocumentRevision>> Generate(
        Guid lineageId, Guid sourceId, IEnumerable<string> lanes, CancellationToken ct)
    {
        if (lineageId == Guid.Empty)
        {
            throw new ArgumentException("A document lineage identifier is required.", nameof(lineageId));
        }

        ArgumentNullException.ThrowIfNull(lanes);
        var result = await Prepare(sourceId, ct).ConfigureAwait(false);
        return result.Map(content => DocumentRevision.Create(lineageId, 1, content.Origin, content.Content, lanes));
    }

    /// <summary>Generates a replacement from current source and template data.</summary>
    /// <param name="previous">The current document revision.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Historical and current snapshots to save together, or a source or lifecycle failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the previous revision is null.</exception>
    /// <exception cref="OverflowException">Thrown when the next revision number would overflow.</exception>
    public async ValueTask<Result<(DocumentRevision Previous, DocumentRevision Current)>> Regenerate(
        DocumentRevision previous, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (!previous.CanRegenerate)
        {
            return Result.Conflict<(DocumentRevision, DocumentRevision)>("The current document revision cannot be replaced.");
        }

        var result = await Prepare(previous.Origin.SourceId, ct).ConfigureAwait(false);
        return result.Map(content => previous.Replace(content.Origin, content.Content));
    }

    /// <summary>Reads current provenance to check a review decision or finalization.</summary>
    /// <param name="sourceId">The application's source identity.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The current source and template versions, or the source failure.</returns>
    /// <exception cref="ArgumentException">Thrown when the source identifier is empty.</exception>
    public async ValueTask<Result<DocumentOrigin>> ReadCurrentOrigin(Guid sourceId, CancellationToken ct)
    {
        var read = await ReadSource(sourceId, ct).ConfigureAwait(false);
        return read.Bind(snapshot => DescribeOrigin(sourceId, snapshot.Version));
    }

    private async ValueTask<Result<(DocumentOrigin Origin, DocumentContent Content)>> Prepare(Guid sourceId, CancellationToken ct)
    {
        var read = await ReadSource(sourceId, ct).ConfigureAwait(false);
        return read.Bind(snapshot => DescribeOrigin(sourceId, snapshot.Version)
            .Map(origin => (origin, template.Create(snapshot.Data))));
    }

    private ValueTask<Result<(string Version, TSource Data)>> ReadSource(Guid sourceId, CancellationToken ct)
    {
        if (sourceId == Guid.Empty)
        {
            throw new ArgumentException("A document source identifier is required.", nameof(sourceId));
        }

        return source.Read(sourceId, ct);
    }

    private Result<DocumentOrigin> DescribeOrigin(Guid sourceId, string sourceVersion)
    {
        var origin = new DocumentOrigin(sourceId, sourceVersion, template.TemplateId, template.TemplateVersion);
        return string.IsNullOrWhiteSpace(origin.SourceVersion) || string.IsNullOrWhiteSpace(origin.TemplateId) ||
            string.IsNullOrWhiteSpace(origin.TemplateVersion)
            ? Result.Invalid<DocumentOrigin>("Document provenance is incomplete.", "origin",
                "A source version and template identity are required.")
            : Result.Ok(origin);
    }
}
