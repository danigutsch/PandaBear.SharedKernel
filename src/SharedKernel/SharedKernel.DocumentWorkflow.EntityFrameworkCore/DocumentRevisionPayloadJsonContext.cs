using System.Text.Json.Serialization;

namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Provides serialization metadata for document workflow storage.</summary>
[JsonSerializable(typeof(DocumentRevisionPayload))]
internal sealed partial class DocumentRevisionPayloadJsonContext : JsonSerializerContext;
