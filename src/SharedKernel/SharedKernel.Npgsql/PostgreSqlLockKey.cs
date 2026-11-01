namespace PandaBear.Npgsql;

/// <summary>A signed 64-bit PostgreSQL advisory lock key. Every numeric value, including zero, is valid.</summary>
/// <param name="Value">The exact advisory lock key sent to PostgreSQL.</param>
public readonly record struct PostgreSqlLockKey(long Value) : IComparable<PostgreSqlLockKey>
{
    /// <summary>Derives a key from an exact caller-owned identity using the existing UTF-8 SHA-256 big-endian protocol.</summary>
    /// <param name="identity">The unchanged identity; no trimming or normalization is applied.</param>
    /// <returns>The signed 64-bit key.</returns>
    /// <exception cref="ArgumentNullException">The identity is null.</exception>
    /// <remarks>Truncating the hash to 64 bits does not guarantee collision-free keys.</remarks>
    public static PostgreSqlLockKey FromIdentity(string identity) => new(PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(identity));

    /// <summary>Compares two keys in PostgreSQL signed numeric order.</summary>
    public static bool operator <(PostgreSqlLockKey left, PostgreSqlLockKey right) => left.Value < right.Value;

    /// <summary>Compares two keys in PostgreSQL signed numeric order.</summary>
    public static bool operator <=(PostgreSqlLockKey left, PostgreSqlLockKey right) => left.Value <= right.Value;

    /// <summary>Compares two keys in PostgreSQL signed numeric order.</summary>
    public static bool operator >(PostgreSqlLockKey left, PostgreSqlLockKey right) => left.Value > right.Value;

    /// <summary>Compares two keys in PostgreSQL signed numeric order.</summary>
    public static bool operator >=(PostgreSqlLockKey left, PostgreSqlLockKey right) => left.Value >= right.Value;

    /// <inheritdoc />
    public int CompareTo(PostgreSqlLockKey other) => Value.CompareTo(other.Value);
}
