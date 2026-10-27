namespace PandaBear.ObjectStorage;

/// <summary>
/// Represents stored object content opened for reading.
/// </summary>
/// <param name="ObjectKey">The application-owned object key.</param>
/// <param name="Content">The readable object content stream.</param>
/// <param name="ContentType">The stored content type.</param>
/// <param name="Length">The object length in bytes.</param>
/// <param name="Checksum">The optional stored checksum.</param>
public sealed record ObjectReadResult(
    string ObjectKey,
    Stream Content,
    string ContentType,
    long Length,
    string? Checksum = null) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => Content.Dispose();
}
