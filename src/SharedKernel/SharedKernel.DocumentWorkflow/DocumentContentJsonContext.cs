using System.Text.Json.Serialization;

namespace PandaBear.DocumentWorkflow;

/// <summary>Defines the canonical structured content encoding for review hashes.</summary>
[JsonSerializable(typeof(DocumentReviewFingerprint))]
internal sealed partial class DocumentContentJsonContext : JsonSerializerContext;
