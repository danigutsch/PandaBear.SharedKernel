namespace SharedKernel.Testing.ObjectStorage;

/// <summary>A write that <see cref="InMemoryObjectStore" /> completed, and how its content stream looked when the store received it.</summary>
/// <param name="ObjectKey">The object key of the request.</param>
/// <param name="ContentType">The content type of the request.</param>
/// <param name="DeclaredLength">The length the request declared.</param>
/// <param name="Checksum">The checksum of the request, if any.</param>
/// <param name="Metadata">A copy of the metadata of the request, if any.</param>
/// <param name="StartPosition">The position of the content stream when the store received it, or <see langword="null" /> when the stream cannot seek.</param>
/// <param name="ContentCanWrite">Whether the content stream accepted writes when the store received it.</param>
/// <param name="ContentStreamType">The runtime type of the content stream.</param>
/// <param name="BytesRead">The number of bytes the store read from the content stream, starting at its position.</param>
/// <param name="Content">The bytes the store read, or empty when the store discards writes.</param>
public sealed record RecordedObjectPut(
    string ObjectKey,
    string ContentType,
    long DeclaredLength,
    string? Checksum,
    IReadOnlyDictionary<string, string>? Metadata,
    long? StartPosition,
    bool ContentCanWrite,
    Type ContentStreamType,
    long BytesRead,
    ReadOnlyMemory<byte> Content);
