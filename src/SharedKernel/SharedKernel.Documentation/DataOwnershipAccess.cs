namespace PandaBear.Documentation;

/// <summary>A labelled service-to-store connection in a data ownership diagram.</summary>
/// <param name="serviceId">The consuming service's node identifier.</param>
/// <param name="storeId">The accessed store's node identifier.</param>
/// <param name="label">The kind of access shown on the connection.</param>
public sealed class DataOwnershipAccess(string serviceId, string storeId, string label)
{
    /// <summary>Gets the consuming service's node identifier.</summary>
    public string ServiceId { get; } = serviceId;

    /// <summary>Gets the accessed store's node identifier.</summary>
    public string StoreId { get; } = storeId;

    /// <summary>Gets the kind of access shown on the connection.</summary>
    public string Label { get; } = label;
}
