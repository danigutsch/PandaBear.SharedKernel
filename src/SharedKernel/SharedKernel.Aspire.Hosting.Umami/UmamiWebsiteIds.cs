using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aspire.Hosting;

/// <summary>Makes and checks Umami website ids.</summary>
internal static class UmamiWebsiteIds
{
    /// <summary>The namespace of the name-based website ids of this package, a random version 4 UUID chosen once.</summary>
    private static readonly Guid WebsiteNamespace = new("6db8a002-69ef-4225-8c66-e486cc8299d0");

    /// <summary>
    /// Creates the website id of a website: the name-based UUID of <c>{umami}/{website}/{domain}</c>, with the resource names in upper
    /// case because Aspire resource names do not differ by case.
    /// </summary>
    /// <param name="umamiName">The name of the Umami resource.</param>
    /// <param name="websiteName">The name of the website resource.</param>
    /// <param name="domain">The website's domain.</param>
    /// <returns>The website id.</returns>
    public static Guid Create(string umamiName, string websiteName, string domain) =>
        NameBased(WebsiteNamespace, string.Create(CultureInfo.InvariantCulture, $"{umamiName.ToUpperInvariant()}/{websiteName.ToUpperInvariant()}/{domain}"));

    /// <summary>
    /// Creates a name-based UUID of version 5 (RFC 9562, section 5.5): the first 16 bytes of the SHA-1 hash of the namespace id in network
    /// byte order followed by the UTF-8 name, with the version set to 5 and the variant to the one of RFC 9562.
    /// </summary>
    /// <param name="namespaceId">The namespace id.</param>
    /// <param name="name">The name.</param>
    /// <returns>The UUID.</returns>
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "RFC 9562 defines version 5 over SHA-1; the hash names an id and protects nothing.")]
    public static Guid NameBased(Guid namespaceId, string name)
    {
        var input = new byte[16 + Encoding.UTF8.GetByteCount(name)];
        namespaceId.TryWriteBytes(input, bigEndian: true, out _);
        Encoding.UTF8.GetBytes(name, input.AsSpan(16));
        Span<byte> hash = stackalloc byte[SHA1.HashSizeInBytes];
        SHA1.HashData(input, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }

    /// <summary>Returns whether Umami 3.4 accepts <paramref name="id"/> as a website id: a UUID of version 1 to 8 with the variant of RFC 9562.</summary>
    /// <param name="id">The id.</param>
    /// <returns><see langword="true"/> when Umami accepts it.</returns>
    public static bool IsAccepted(Guid id) => id.Version is >= 1 and <= 8 && (id.Variant & 0xC) == 0x8;
}
