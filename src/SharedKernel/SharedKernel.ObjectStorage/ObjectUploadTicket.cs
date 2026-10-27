namespace PandaBear.ObjectStorage;

/// <summary>
/// Describes a time-limited direct upload ticket.
/// </summary>
/// <param name="ObjectKey">The application-owned object key.</param>
/// <param name="UploadUri">The upload URI.</param>
/// <param name="ExpiresAt">The upload ticket expiration time.</param>
/// <param name="RequiredHeaders">The headers that clients must include when uploading.</param>
public sealed record ObjectUploadTicket(
    string ObjectKey,
    Uri UploadUri,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);
