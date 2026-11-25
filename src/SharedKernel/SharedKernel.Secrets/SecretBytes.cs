using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace SharedKernel.Secrets;

/// <summary>
/// Decrypted bytes that do not print themselves. <see cref="ToString"/> never shows the content, the content lives in a
/// pinned array that the garbage collector does not move, and <see cref="Dispose"/> overwrites it. .NET cannot guarantee
/// more: an immutable <see cref="string"/> made from the bytes, a copy written to a stream or a file, and swap or core dumps
/// are outside this type's control. Keep the instance short-lived and dispose it.
/// </summary>
[DebuggerDisplay("SecretBytes (redacted)")]
public sealed class SecretBytes : IDisposable
{
    private byte[]? _bytes;

    private SecretBytes(byte[] bytes) => _bytes = bytes;

    /// <summary>Gets the number of bytes, or 0 after disposal.</summary>
    public int Length => _bytes?.Length ?? 0;

    /// <summary>Gets a value indicating whether the bytes were overwritten and released.</summary>
    public bool IsDisposed => _bytes is null;

    /// <summary>Copies <paramref name="bytes"/> into a new pinned array. The source stays the caller's to clear.</summary>
    /// <param name="bytes">The bytes to keep.</param>
    /// <returns>The holder.</returns>
    public static SecretBytes CopyOf(ReadOnlySpan<byte> bytes)
    {
        var copy = GC.AllocateUninitializedArray<byte>(bytes.Length, pinned: true);
        bytes.CopyTo(copy);
        return new SecretBytes(copy);
    }

    /// <summary>Takes ownership of <paramref name="pinned"/>, which the caller must not use again.</summary>
    internal static SecretBytes Adopt(byte[] pinned) => new(pinned);

    /// <summary>Returns the bytes. The span is valid until the instance is disposed.</summary>
    /// <exception cref="ObjectDisposedException">The instance was disposed.</exception>
    public ReadOnlySpan<byte> AsSpan() => _bytes ?? throw new ObjectDisposedException(nameof(SecretBytes));

    /// <summary>Writes the bytes to <paramref name="destination"/>.</summary>
    /// <param name="destination">The stream; the caller owns what it then holds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The instance was disposed.</exception>
    public void WriteTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(AsSpan());
    }

    /// <summary>Decodes the bytes as UTF-8 into an immutable string that cannot be cleared. Avoid it where the bytes will do.</summary>
    /// <exception cref="ObjectDisposedException">The instance was disposed.</exception>
    public string RevealUtf8() => Encoding.UTF8.GetString(AsSpan());

    /// <summary>Returns a fixed text; the content is never shown.</summary>
    public override string ToString() => "SecretBytes(redacted)";

    /// <summary>Overwrites the bytes and releases them. Calling it again does nothing.</summary>
    public void Dispose()
    {
        var bytes = _bytes;
        _bytes = null;
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
