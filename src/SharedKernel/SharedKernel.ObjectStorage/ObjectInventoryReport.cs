// Adapted from ViajantesTurismo. See NOTICE.ViajantesTurismo.txt for the MIT license and attribution.

namespace PandaBear.ObjectStorage;

/// <summary>Compares observed object inventory with the consuming application's live references.</summary>
public sealed class ObjectInventoryReport
{
    private ObjectInventoryReport(string[] missingObjectKeys, ObjectInventoryItem[] orphanObjects)
    {
        MissingObjectKeys = Array.AsReadOnly(missingObjectKeys);
        OrphanObjects = Array.AsReadOnly(orphanObjects);
    }

    /// <summary>Gets the referenced keys absent from the observed inventory, in ordinal order.</summary>
    public IReadOnlyList<string> MissingObjectKeys { get; }

    /// <summary>Gets observed objects without live references, in ordinal key order.</summary>
    public IReadOnlyList<ObjectInventoryItem> OrphanObjects { get; }

    /// <summary>Compares one inventory and reference snapshot without changing either source.</summary>
    /// <param name="inventory">Observed objects. The first observation of each key supplies its modification time.</param>
    /// <param name="referencedKeys">The application's live object references for the same scope.</param>
    /// <returns>The missing keys and unreferenced observations. The host owns any deletion decision.</returns>
    /// <exception cref="ArgumentNullException">An input collection or inventory item is null.</exception>
    public static ObjectInventoryReport Compare(IEnumerable<ObjectInventoryItem> inventory, IEnumerable<string> referencedKeys)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(referencedKeys);

        var references = referencedKeys.Where(key => !string.IsNullOrWhiteSpace(key)).ToHashSet(StringComparer.Ordinal);
        var observed = new Dictionary<string, ObjectInventoryItem>(StringComparer.Ordinal);
        foreach (var item in inventory)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (!string.IsNullOrWhiteSpace(item.ObjectKey))
            {
                observed.TryAdd(item.ObjectKey, item);
            }
        }

        return new ObjectInventoryReport(
            [.. references.Where(key => !observed.ContainsKey(key)).Order(StringComparer.Ordinal)],
            [.. observed.Values.Where(item => !references.Contains(item.ObjectKey)).OrderBy(item => item.ObjectKey, StringComparer.Ordinal)]);
    }
}
