using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace PandaBear.Testing.AspNetCore;

/// <summary>
/// An <see cref="IFormFile" /> over a caller-supplied stream, for upload tests that need a non-seekable, slow or failing source.
/// Unlike <see cref="FakeFormFile" />, it never buffers, rewinds or wraps the stream.
/// </summary>
/// <remarks>
/// <see cref="OpenReadStream" /> returns the same stream on every call, and the copy methods read it from its current position,
/// so the content can be consumed only once when the stream is non-seekable. The caller owns the stream; code under test that
/// disposes the opened stream disposes it. <see cref="Length" /> is the declared length and may differ from the stream content,
/// which lets tests exercise a client that misstates the size.
/// </remarks>
public sealed class StreamFormFile : IFormFile
{
    private readonly Stream _content;

    /// <summary>Initializes a new instance of the <see cref="StreamFormFile" /> class.</summary>
    /// <param name="content">The stream handed out unchanged by <see cref="OpenReadStream" />.</param>
    /// <param name="length">The declared file length in bytes.</param>
    /// <param name="fileName">The client-supplied file name.</param>
    /// <param name="contentType">The declared content type.</param>
    /// <param name="name">The form field name.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/>, <paramref name="fileName"/>, <paramref name="contentType"/>, or <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative.</exception>
    public StreamFormFile(Stream content, long length, string fileName, string contentType = "application/octet-stream", string name = "file")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(name);

        _content = content;
        Length = length;
        FileName = fileName;
        ContentType = contentType;
        Name = name;
    }

    /// <inheritdoc />
    public string ContentType { get; }

    /// <inheritdoc />
    public string ContentDisposition => $"form-data; name=\"{Name}\"; filename=\"{FileName}\"";

    /// <inheritdoc />
    public IHeaderDictionary Headers => new HeaderDictionary
    {
        [HeaderNames.ContentType] = ContentType,
        [HeaderNames.ContentDisposition] = ContentDisposition,
    };

    /// <inheritdoc />
    public long Length { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string FileName { get; }

    /// <inheritdoc />
    public Stream OpenReadStream() => _content;

    /// <inheritdoc />
    public void CopyTo(Stream target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _content.CopyTo(target);
    }

    /// <inheritdoc />
    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        return _content.CopyToAsync(target, cancellationToken);
    }
}
