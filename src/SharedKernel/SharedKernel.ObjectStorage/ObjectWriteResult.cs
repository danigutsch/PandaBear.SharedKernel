namespace PandaBear.ObjectStorage;

/// <summary>
/// Describes a stored object.
/// </summary>
/// <param name="ObjectKey">The application-owned object key.</param>
/// <param name="StorageUri">The provider storage URI.</param>
/// <param name="PublicUri">The public or CDN URI.</param>
/// <param name="ContentType">The stored content type.</param>
/// <param name="Length">The stored length in bytes.</param>
/// <param name="Checksum">The optional content checksum.</param>
public sealed record ObjectWriteResult(
    string ObjectKey,
    Uri StorageUri,
    Uri PublicUri,
    string ContentType,
    long Length,
    string? Checksum);
