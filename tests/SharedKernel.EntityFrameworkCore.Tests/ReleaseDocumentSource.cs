using SharedKernel.DocumentRendering;
using SharedKernel.DocumentWorkflow;
using SharedKernel.Results;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>A neutral release-note source and template.</summary>
internal sealed class ReleaseDocumentSource : IDocumentSource<string>, IDocumentTemplate<string>
{
    /// <inheritdoc />
    public string TemplateId => "release-note";

    /// <inheritdoc />
    public string TemplateVersion => "1";

    /// <inheritdoc />
    public ValueTask<Result<(string Version, string Data)>> Read(Guid sourceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Ok(("build-1", "ready")));
    }

    /// <inheritdoc />
    public DocumentContent Create(string source) => new(
        "en", "Release note", "Build",
        [new DocumentContentField("summary", "Summary", source, DocumentPrivacyClassification.Public, true)],
        new DocumentBrandingSnapshot("1", "Release office", null));
}
