using System.Text.Json.Serialization;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Exercises request constructor compatibility through AOT-safe JSON metadata.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ObjectUploadRequest))]
internal sealed partial class ObjectStorageRequestJsonContext : JsonSerializerContext;
