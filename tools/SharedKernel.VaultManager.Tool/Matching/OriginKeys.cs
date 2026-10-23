using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;

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
/// the first 16 lowercase hex characters of SHA-256 over the UTF-8 bytes of the normalized type number, title, and first
/// host, joined with line feeds. Only the hash is stored, never the values. The user name stays out of the hash, so a
/// decisions file cannot confirm a guessed user name.
/// </para>
/// <para>
/// When several items share a key, the first one in input order keeps it, and each later one gets the next free suffix
/// of <c>#2</c>, <c>#3</c>, and so on. A suffixed key is free when no other item has it, either as its own key, for
/// example because an original identifier ends in <c>#2</c>, or as the suffixed key of an item before it. The same
/// files therefore produce the same unique keys on every run.
/// </para>
/// </remarks>
internal static class OriginKeys
{
    /// <summary>Computes the origin key of every item.</summary>
    /// <param name="items">Items in input order.</param>
    /// <returns>Unique keys aligned with <paramref name="items"/>.</returns>
    public static IReadOnlyList<string> Compute(IReadOnlyList<VaultItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        string[] keys = [.. items.Select(BaseKey)];
        var taken = new HashSet<string>(keys, StringComparer.Ordinal);
        var kept = new HashSet<string>(StringComparer.Ordinal);
        var nextSuffix = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < keys.Length; index++)
        {
            string key = keys[index];
            if (!kept.Add(key))
            {
                keys[index] = Unique(key, taken, nextSuffix.GetValueOrDefault(key, 2), out int suffix);
                nextSuffix[key] = suffix + 1;
            }
        }

        return keys;
    }

    /// <summary>
    /// Returns <paramref name="key"/> when no other item has it, or else the key with the first free suffix of <c>#2</c>,
    /// <c>#3</c>, and so on, and records the returned key as taken.
    /// </summary>
    /// <param name="key">Key the item would have.</param>
    /// <param name="taken">Keys other items already have; receives the returned key.</param>
    /// <returns>A key no other item has.</returns>
    public static string MakeUnique(string key, ISet<string> taken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(taken);

        return taken.Add(key) ? key : Unique(key, taken, 2, out _);
    }

    private static string Unique(string key, ISet<string> taken, int firstSuffix, out int suffix)
    {
        for (suffix = firstSuffix; ; suffix++)
        {
            string candidate = string.Create(CultureInfo.InvariantCulture, $"{key}#{suffix}");
            if (taken.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string BaseKey(VaultItem item)
    {
        var origin = item.Origin;
        string prefix = origin.Provider + "|" + origin.SourceFile + "|";
        return string.IsNullOrEmpty(origin.OriginalId) ? prefix + "content:" + ContentHash(item) : prefix + origin.OriginalId;
    }

    // The host is read from the masked address: ItemNormalizer keeps an address whole when its scheme is not http or https,
    // user info and query included, and a key that hashes a credential would let a reader confirm a guessed one.
    private static string ContentHash(VaultItem item)
    {
        string? firstHost = item.Login?.Uris.Select(uri => ItemNormalizer.Host(ShownValues.MaskUri(uri.Value))).FirstOrDefault(host => host is not null);
        string content = string.Join(
            '\n',
            ((int)item.Type).ToString(CultureInfo.InvariantCulture),
            ItemNormalizer.Title(item.Name),
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
