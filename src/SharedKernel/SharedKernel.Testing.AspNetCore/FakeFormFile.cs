using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace PandaBear.Testing.AspNetCore;

/// <summary>An in-memory <see cref="IFormFile" /> for binding and upload tests.</summary>
public sealed class FakeFormFile : IFormFile
{
    private readonly byte[] _content;

    /// <summary>Initializes a new instance of the <see cref="FakeFormFile" /> class.</summary>
    /// <param name="content">The file content.</param>
    /// <param name="fileName">The client-supplied file name.</param>
    /// <param name="contentType">The declared content type.</param>
    /// <param name="name">The form field name.</param>
    public FakeFormFile(byte[] content, string fileName, string contentType = "application/octet-stream", string name = "file")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(name);

        _content = content;
        FileName = fileName;
        ContentType = contentType;
        Name = name;
    }

    /// <inheritdoc />
    public string ContentType
    {
        get;
    }

    /// <inheritdoc />
    public string ContentDisposition => $"form-data; name=\"{Name}\"; filename=\"{FileName}\"";

    /// <inheritdoc />
    public IHeaderDictionary Headers => new HeaderDictionary
    {
        [HeaderNames.ContentType] = ContentType,
        [HeaderNames.ContentDisposition] = ContentDisposition,
    };

    /// <inheritdoc />
    public long Length => _content.LongLength;

    /// <inheritdoc />
    public string Name
    {
        get;
    }

    /// <inheritdoc />
    public string FileName
    {
        get;
    }

    /// <inheritdoc />
    public Stream OpenReadStream() => new MemoryStream(_content, writable: false);

    /// <inheritdoc />
    public void CopyTo(Stream target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Write(_content);
    }

    /// <inheritdoc />
    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.WriteAsync(_content, cancellationToken).AsTask();
    }
}
