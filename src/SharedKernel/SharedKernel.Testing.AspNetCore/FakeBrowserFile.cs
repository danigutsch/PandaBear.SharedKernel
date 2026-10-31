using Microsoft.AspNetCore.Components.Forms;

namespace PandaBear.Testing.AspNetCore;

/// <summary>An in-memory <see cref="IBrowserFile" /> that enforces the maximum allowed size like the real implementation.</summary>
public sealed class FakeBrowserFile : IBrowserFile
{
    /// <summary>The <c>maxAllowedSize</c> that <see cref="OpenReadStream" /> uses when none is supplied; matches the <see cref="IBrowserFile" /> default.</summary>
    public const long DefaultMaxAllowedSize = 512000;

    private readonly byte[] _content;

    /// <summary>Initializes a new instance of the <see cref="FakeBrowserFile" /> class.</summary>
    /// <param name="content">The file content.</param>
    /// <param name="name">The client-supplied file name.</param>
    /// <param name="contentType">The declared content type.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> or <paramref name="name"/> or <paramref name="contentType"/> is <see langword="null"/>.</exception>
    public FakeBrowserFile(byte[] content, string name, string contentType = "application/octet-stream")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contentType);

        _content = content;
        Name = name;
        ContentType = contentType;
    }

    /// <inheritdoc />
    public string Name
    {
        get;
    }

    /// <inheritdoc />
    public DateTimeOffset LastModified
    {
        get;
        init;
    }

    /// <summary>Gets the size reported by <see cref="Size" /> when it should differ from the content length; the content length when <see langword="null" />.</summary>
    public long? DeclaredSize
    {
        get;
        init;
    }

    /// <summary>Gets the exception thrown by <see cref="OpenReadStream" /> after the limit is recorded; none when <see langword="null" />.</summary>
    public Exception? OpenFailure
    {
        get;
        init;
    }

    /// <summary>Gets the size limit passed to the latest <see cref="OpenReadStream" /> call, or <see langword="null" /> if never opened.</summary>
    public long? MaximumAllowedSize
    {
        get;
        private set;
    }

    /// <inheritdoc />
    public long Size => DeclaredSize ?? _content.LongLength;

    /// <inheritdoc />
    public string ContentType
    {
        get;
    }

    /// <inheritdoc />
    public Stream OpenReadStream(long maxAllowedSize = DefaultMaxAllowedSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        MaximumAllowedSize = maxAllowedSize;
        if (OpenFailure is not null)
        {
            throw OpenFailure;
        }

        if (Size > maxAllowedSize)
        {
            throw new IOException($"Supplied file with size {Size} bytes exceeds the maximum of {maxAllowedSize} bytes.");
        }

        return new MemoryStream(_content, writable: false);
    }
}
