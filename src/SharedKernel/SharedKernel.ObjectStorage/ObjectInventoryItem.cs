namespace PandaBear.ObjectStorage;

/// <summary>
/// Describes a stored object during reconciliation.
/// </summary>
/// <param name="ObjectKey">The application-owned object key.</param>
/// <param name="LastModifiedAt">The last observed object modification time.</param>
public sealed record ObjectInventoryItem(string ObjectKey, DateTimeOffset LastModifiedAt);
