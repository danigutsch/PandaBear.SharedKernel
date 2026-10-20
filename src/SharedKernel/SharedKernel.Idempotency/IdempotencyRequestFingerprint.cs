using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PandaBear.Idempotency;

/// <summary>Creates deterministic fingerprints from ordered request components without retaining their values.</summary>
public static class IdempotencyRequestFingerprint
{
    /// <summary>Hashes ordered, length-framed string components with SHA-256.</summary>
    /// <param name="components">The non-null components, in their contract-defined order. Empty strings are allowed.</param>
    /// <returns>The uppercase hexadecimal SHA-256 hash of the UTF-8 encoded framing.</returns>
    /// <remarks>
    /// Each component is framed as its invariant decimal UTF-16 length, a colon, and its unchanged value.
    /// Frames are separated by a colon. No trimming or Unicode normalization is performed.
    /// An empty component list hashes the empty string. Hashing is not encryption and does not conceal low-entropy inputs.
    /// </remarks>
    public static string Create(params string[] components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var framed = new StringBuilder();
        foreach (var component in components)
        {
            ArgumentNullException.ThrowIfNull(component);
            if (framed.Length > 0)
            {
                framed.Append(':');
            }

            framed.Append(component.Length.ToString(CultureInfo.InvariantCulture));
            framed.Append(':');
            framed.Append(component);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(framed.ToString())));
    }
}
