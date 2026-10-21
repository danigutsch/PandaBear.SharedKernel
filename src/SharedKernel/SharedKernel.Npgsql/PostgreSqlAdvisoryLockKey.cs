using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PandaBear.Npgsql;

/// <summary>
/// Derives deterministic PostgreSQL advisory lock keys from caller-owned identities.
/// </summary>
public static class PostgreSqlAdvisoryLockKey
{
    /// <summary>
    /// Hashes the exact UTF-8 identity with SHA-256 and reads the first eight bytes as a signed big-endian key.
    /// </summary>
    /// <param name="identity">The caller-composed lock identity, without implicit trimming or normalization.</param>
    /// <returns>The signed 64-bit advisory lock key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity" /> is null.</exception>
    /// <remarks>
    /// Callers own identity prefixes, formatting, and validation. Empty identities are supported.
    /// Truncating the hash to 64 bits does not guarantee collision-free keys.
    /// </remarks>
    public static long FromUtf8Sha256BigEndian(string identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }
}
