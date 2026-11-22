// Adapted from ViajantesTurismo. See NOTICE.ViajantesTurismo.txt for the MIT license and attribution.

using System.Buffers;
using System.Security.Cryptography;

namespace PandaBear.ObjectStorage.FileSystem;

/// <summary>Copies and verifies the declared object bytes without buffering the object in memory.</summary>
internal static class VerifiedObjectContent
{
    private const string Sha256Prefix = "sha256:";
    private const int BufferSize = 81_920;
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");

    /// <summary>Reads the supplied SHA-256 checksum, if present.</summary>
    /// <exception cref="ArgumentException">The checksum is not a SHA-256 hex digest.</exception>
    internal static byte[]? ParseChecksum(string? checksum)
    {
        if (string.IsNullOrEmpty(checksum))
        {
            return null;
        }

        var hex = checksum.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase)
            ? checksum.AsSpan(Sha256Prefix.Length)
            : default;
        if (hex.Length != SHA256.HashSizeInBytes * 2 || hex.IndexOfAnyExcept(HexDigits) >= 0)
        {
            throw new ArgumentException("The checksum must be sha256: followed by 64 hexadecimal digits.", nameof(checksum));
        }

        return Convert.FromHexString(hex);
    }

    /// <summary>Copies all declared bytes and verifies that there are no more bytes and the supplied digest matches.</summary>
    /// <exception cref="InvalidDataException">The length or checksum differs from the input.</exception>
    internal static async ValueTask Copy(Stream source, Stream destination, long length, byte[]? expectedHash, CancellationToken ct)
    {
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        using var hash = expectedHash is not null ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            var remaining = length;
            while (remaining > 0)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(BufferSize, remaining)), ct).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new InvalidDataException("The content is shorter than the declared object length.");
                }

                hash?.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                remaining -= read;
            }

            if (await source.ReadAsync(buffer.AsMemory(0, 1), ct).ConfigureAwait(false) != 0)
            {
                throw new InvalidDataException("The content is longer than the declared object length.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (hash is not null && !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), expectedHash))
        {
            throw new InvalidDataException("The content does not match the supplied checksum.");
        }
    }
}
