namespace PandaBear.ObjectStorage;

/// <summary>
/// Describes a direct upload request.
/// </summary>
/// <param name="ObjectKey">The application-owned object key.</param>
/// <param name="ContentType">The expected content type.</param>
/// <param name="Length">The expected upload length in bytes.</param>
/// <param name="ExpiresAfter">The upload ticket lifetime.</param>
public sealed record ObjectUploadRequest(
    string ObjectKey,
    string ContentType,
    long Length,
    TimeSpan ExpiresAfter)
{
    /// <summary>Creates a request using a validated object identity.</summary>
    /// <param name="objectKey">The objectKey value.</param>
    /// <param name="contentType">The contentType value.</param>
    /// <param name="length">The length value.</param>
    /// <param name="expiresAfter">The expiresAfter value.</param>
    /// <returns>The provider-neutral request.</returns>
    /// <exception cref="InvalidOperationException">The key has not been initialized.</exception>
    public static ObjectUploadRequest Create(ObjectStorageKey objectKey, string contentType, long length, TimeSpan expiresAfter) =>
        new(objectKey.Value, contentType, length, expiresAfter);
}
