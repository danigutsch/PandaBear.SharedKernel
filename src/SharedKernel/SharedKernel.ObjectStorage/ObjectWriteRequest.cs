namespace PandaBear.ObjectStorage;

/// <summary>
/// Describes an object write.
/// </summary>
/// <param name="ObjectKey">The application-owned object key.</param>
/// <param name="Content">The object content stream.</param>
/// <param name="ContentType">The content type.</param>
/// <param name="Length">The object length in bytes.</param>
/// <param name="Checksum">The optional content checksum.</param>
/// <param name="Metadata">The optional object metadata.</param>
public sealed record ObjectWriteRequest(
    string ObjectKey,
    Stream Content,
    string ContentType,
    long Length,
    string? Checksum = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    /// <summary>Creates a request using a validated object identity.</summary>
    /// <param name="objectKey">The objectKey value.</param>
    /// <param name="content">The content value.</param>
    /// <param name="contentType">The contentType value.</param>
    /// <param name="length">The length value.</param>
    /// <param name="checksum">The checksum value.</param>
    /// <param name="metadata">The metadata value.</param>
    /// <returns>The provider-neutral request.</returns>
    /// <exception cref="InvalidOperationException">The key has not been initialized.</exception>
    public static ObjectWriteRequest Create(ObjectStorageKey objectKey, Stream content, string contentType, long length, string? checksum = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        new(objectKey.Value, content, contentType, length, checksum, metadata);
}
