namespace PandaBear.Testing.ObjectStorage;

/// <summary>An object held by <see cref="InMemoryObjectStore" />.</summary>
/// <param name="ObjectKey">The object key.</param>
/// <param name="Content">The stored bytes.</param>
/// <param name="ContentType">The stored content type.</param>
/// <param name="Checksum">The stored checksum, if any.</param>
/// <param name="Metadata">The stored metadata; empty when the write carried none.</param>
/// <param name="LastModifiedAt">The time of the last write, or the time set by <see cref="InMemoryObjectStore.SetLastModified" />.</param>
public sealed record StoredObject(
    string ObjectKey,
    ReadOnlyMemory<byte> Content,
    string ContentType,
    string? Checksum,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset LastModifiedAt)
{
    /// <summary>Gets the stored length in bytes.</summary>
    public long Length => Content.Length;
}
