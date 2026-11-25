using System.Text;

namespace SharedKernel.DocumentWorkflow.Tests;

/// <summary>Supplies a neutral release-note source and template.</summary>
internal sealed class ReleaseNoteConsumer : IDocumentSource<string>, IDocumentTemplate<string>, IDocumentArtifactStore
{
    private readonly Dictionary<string, string> artifacts = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the current application source data.</summary>
    public string BuildNumber { get; set; } = "19";

    /// <inheritdoc />
    public string TemplateId => "release-note";

    /// <inheritdoc />
    public string TemplateVersion { get; set; } = "1";

    /// <summary>Gets or sets the available source version.</summary>
    public string SourceVersion { get; set; } = "build-1";

    /// <inheritdoc />
    public ValueTask<Result<(string Version, string Data)>> Read(Guid sourceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Ok((SourceVersion, BuildNumber)));
    }

    /// <inheritdoc />
    public DocumentContent Create(string source) => new(
        "en",
        "Release note",
        "Build results",
        [
            new("build-number", "Build number", source, DocumentPrivacyClassification.Operational, false),
            new("summary", "Summary", "Ready for review", DocumentPrivacyClassification.Public, true),
        ],
        new DocumentBrandingSnapshot("1", "Release office", null));
    /// <summary>Stores rendered text once under the private artifact handle.</summary>
    public void StoreArtifact(string handle, string content) => artifacts.Add(handle, content);

    /// <summary>Reads a historical artifact by its immutable handle.</summary>
    public string ReadArtifact(string handle) => artifacts[handle];

    /// <summary>Gets the number of distinct stored artifacts.</summary>
    public int ArtifactCount => artifacts.Count;

    /// <summary>Gets or sets whether the fixture changes the source after storage.</summary>
    public bool ChangeSourceAfterArtifactWrite { get; set; }

    /// <summary>Gets or sets whether storage returns a transient failure.</summary>
    public bool ArtifactStoreUnavailable { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<string>> Write(DocumentArtifactWrite artifact, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (ArtifactStoreUnavailable)
        {
            return ValueTask.FromResult(Result.Unavailable<string>("Artifact storage is unavailable."));
        }

        var format = Uri.EscapeDataString(artifact.ContentType);
        var handle = $"{artifact.LineageId:N}/{artifact.RevisionNumber}/{artifact.Reviewed.Number}/{artifact.Reviewed.ContentHash}/{artifact.ContentHash}/{format}";
        var text = Encoding.UTF8.GetString(artifact.Content.Span);
        if (artifacts.TryGetValue(handle, out var previous) && !string.Equals(previous, text, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(Result.Conflict<string>("The artifact already exists with other content."));
        }

        artifacts.TryAdd(handle, text);
        if (ChangeSourceAfterArtifactWrite)
        {
            SourceVersion = "build-2";
        }

        return ValueTask.FromResult(Result.Ok(handle));
    }
}
