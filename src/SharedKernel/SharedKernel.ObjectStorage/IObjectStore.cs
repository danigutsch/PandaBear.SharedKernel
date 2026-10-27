namespace PandaBear.ObjectStorage;

/// <summary>
/// Stores objects in a provider-neutral object store.
/// </summary>
public interface IObjectStore
{
    /// <summary>
    /// Writes an object to storage.
    /// </summary>
    /// <param name="request">The write request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The stored object result.</returns>
    ValueTask<ObjectWriteResult> Put(ObjectWriteRequest request, CancellationToken ct);

    /// <summary>
    /// Opens a stored object for reading.
    /// </summary>
    /// <param name="objectKey">The application-owned object key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The stored object content and metadata.</returns>
    ValueTask<ObjectReadResult> OpenRead(string objectKey, CancellationToken ct);

    /// <summary>
    /// Returns whether an object exists.
    /// </summary>
    /// <param name="objectKey">The application-owned object key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns><see langword="true" /> when the object exists.</returns>
    ValueTask<bool> Exists(string objectKey, CancellationToken ct);

    /// <summary>
    /// Lists stored object keys below a prefix.
    /// </summary>
    /// <param name="prefix">The application-owned key prefix.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching object keys.</returns>
    ValueTask<IReadOnlyList<string>> ListKeys(string prefix, CancellationToken ct);

    /// <summary>
    /// Lists stored objects below a prefix with cleanup-relevant metadata.
    /// </summary>
    /// <param name="prefix">The application-owned key prefix.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching objects.</returns>
    ValueTask<IReadOnlyList<ObjectInventoryItem>> ListObjects(string prefix, CancellationToken ct);

    /// <summary>
    /// Derives the public URI for an object key without requiring it to be stored in metadata.
    /// </summary>
    /// <param name="objectKey">The application-owned object key.</param>
    /// <returns>The public URI.</returns>
    Uri GetPublicUri(string objectKey);

    /// <summary>
    /// Creates a time-limited upload ticket for direct uploads.
    /// </summary>
    /// <param name="request">The upload request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The upload ticket.</returns>
    ValueTask<ObjectUploadTicket> CreateUploadUrl(ObjectUploadRequest request, CancellationToken ct);

    /// <summary>
    /// Deletes an object if it exists.
    /// </summary>
    /// <param name="objectKey">The application-owned object key.</param>
    /// <param name="ct">The cancellation token.</param>
    ValueTask Delete(string objectKey, CancellationToken ct);
}
