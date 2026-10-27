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
    TimeSpan ExpiresAfter);
