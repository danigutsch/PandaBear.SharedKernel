using System.Text.Json;
using SharedKernel.Results;

namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Stores the revision identity, concurrency token and versioned document data.</summary>
internal sealed class DocumentWorkflowRow
{
    private const int CurrentPayloadVersion = 1;

    private DocumentWorkflowRow()
    {
    }

    /// <summary>Creates a database row for a new document revision.</summary>
    public DocumentWorkflowRow(DocumentRevision revision)
    {
        LineageId = revision.LineageId;
        Number = revision.Number;
        Write(revision);
    }

    /// <summary>Gets the database identity.</summary>
    public long Id { get; private set; }

    /// <summary>Gets the workflow lineage.</summary>
    public Guid LineageId { get; private set; }

    /// <summary>Gets the revision within its lineage.</summary>
    public int Number { get; private set; }

    /// <summary>Gets the optimistic concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Gets the schema version of the payload.</summary>
    public int PayloadVersion { get; private set; }

    /// <summary>Gets the structured serialized state.</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Gets the private artifact handle outside the structured payload.</summary>
    public string? ArtifactReference { get; private set; }

    /// <summary>Replaces the row data with a validated workflow state.</summary>
    /// <exception cref="OverflowException">Thrown when the storage version cannot advance.</exception>
    public void Write(DocumentRevision revision)
    {
        var snapshot = revision.Snapshot();
        Payload = JsonSerializer.Serialize(DocumentRevisionPayload.From(snapshot), DocumentRevisionPayloadJsonContext.Default.DocumentRevisionPayload);
        ArtifactReference = snapshot.ArtifactReference;
        PayloadVersion = CurrentPayloadVersion;
        Version = checked(Version + 1);
    }

    /// <summary>Reads and validates the stored workflow state.</summary>
    public Result<PersistedDocumentRevision> Restore()
    {
        if (PayloadVersion != CurrentPayloadVersion || Version <= 0)
        {
            return InvalidPayload();
        }

        try
        {
            var payload = JsonSerializer.Deserialize(Payload, DocumentRevisionPayloadJsonContext.Default.DocumentRevisionPayload);
            return payload is null || payload.Fields.IsDefault
                ? InvalidPayload()
                : DocumentRevision.Restore(payload.ToSnapshot(LineageId, Number, ArtifactReference))
                    .Map(revision => new PersistedDocumentRevision(revision, Version));
        }
        catch (JsonException)
        {
            return InvalidPayload();
        }
        catch (ArgumentException)
        {
            return InvalidPayload();
        }
    }

    private static Result<PersistedDocumentRevision> InvalidPayload() =>
        Result.Invalid<PersistedDocumentRevision>("Stored document payload is invalid.", "payload",
            "The document payload version and contents must be supported.");
}
