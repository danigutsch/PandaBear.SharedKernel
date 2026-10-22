using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>
/// Origin keys identify an input item across runs, so decisions and overrides can refer to it without storing values.
/// </summary>
/// <remarks>
/// <para>
/// An item with an original identifier has the key <c>provider|sourceFile|originalId</c>.
/// </para>
/// <para>
/// An item without one has the content key <c>provider|sourceFile|content:&lt;hex&gt;</c>, where <c>&lt;hex&gt;</c> is
/// the first 16 lowercase hex characters of SHA-256 over the UTF-8 bytes of the normalized type number, title, user
/// name, and first host, joined with line feeds. Only the hash is stored, never the values.
/// </para>
/// <para>
/// When several items in one input share a key, the second and later ones, in input order, get the suffix <c>#2</c>,
/// <c>#3</c>, and so on. The same files therefore produce the same keys on every run.
/// </para>
/// </remarks>
internal static class OriginKeys
{
    /// <summary>Computes the origin key of every item.</summary>
    /// <param name="items">Items in input order.</param>
    /// <returns>Keys aligned with <paramref name="items"/>.</returns>
    public static IReadOnlyList<string> Compute(IReadOnlyList<VaultItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        string[] keys = new string[items.Count];
        for (int index = 0; index < items.Count; index++)
        {
            string key = BaseKey(items[index]);
            int count = seen.TryGetValue(key, out int previous) ? previous + 1 : 1;
            seen[key] = count;
            keys[index] = count == 1 ? key : string.Create(CultureInfo.InvariantCulture, $"{key}#{count}");
        }

        return keys;
    }

    private static string BaseKey(VaultItem item)
    {
        var origin = item.Origin;
        string prefix = origin.Provider + "|" + origin.SourceFile + "|";
        return string.IsNullOrEmpty(origin.OriginalId) ? prefix + "content:" + ContentHash(item) : prefix + origin.OriginalId;
    }

    private static string ContentHash(VaultItem item)
    {
        string? firstHost = item.Login?.Uris.Select(uri => ItemNormalizer.Host(uri.Value)).FirstOrDefault(host => host is not null);
        string content = string.Join(
            '\n',
            ((int)item.Type).ToString(CultureInfo.InvariantCulture),
            ItemNormalizer.Title(item.Name),
            ItemNormalizer.Username(item.Login?.Username),
            firstHost ?? string.Empty);
        return Hex16(content);
    }

    /// <summary>First 16 lowercase hex characters of SHA-256 over the UTF-8 bytes of a text.</summary>
    /// <param name="text">Text to hash.</param>
    /// <returns>The hex prefix.</returns>
    public static string Hex16(string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexStringLower(hash, 0, 8);
    }
}
