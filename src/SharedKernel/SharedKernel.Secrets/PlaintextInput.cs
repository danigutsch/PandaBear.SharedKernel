using System.Security.Cryptography;

namespace PandaBear.Secrets;

/// <summary>Reads standard input into one array, up to a limit, and clears it afterwards.</summary>
internal static class PlaintextInput
{
    /// <summary>The most bytes <c>encrypt</c> reads.</summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>Reads the stream to its end.</summary>
    /// <exception cref="InvalidOperationException">The stream is empty or longer than the limit.</exception>
    public static async Task<byte[]> Read(Stream input, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxBytes + 1];
        var length = 0;
        int read;
        try
        {
            while (length < buffer.Length && (read = await input.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                length += read;
            }

            if (length is 0 or > MaxBytes)
            {
                throw new InvalidOperationException("Standard input must hold between 1 byte and 16 MiB of plaintext.");
            }

            return buffer[..length];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
