using Microsoft.AspNetCore.Components.Forms;

namespace PandaBear.Testing.AspNetCore;

/// <summary>An in-memory <see cref="IBrowserFile" /> that enforces the maximum allowed size like the real implementation.</summary>
public sealed class FakeBrowserFile : IBrowserFile
{
    private readonly byte[] _content;

    /// <summary>Initializes a new instance of the <see cref="FakeBrowserFile" /> class.</summary>
    /// <param name="content">The file content.</param>
    /// <param name="name">The client-supplied file name.</param>
    /// <param name="contentType">The declared content type.</param>
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

    /// <inheritdoc />
    public long Size => _content.LongLength;

    /// <inheritdoc />
    public string ContentType
    {
        get;
    }

    /// <inheritdoc />
    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        if (Size > maxAllowedSize)
        {
            throw new IOException($"Supplied file with size {Size} bytes exceeds the maximum of {maxAllowedSize} bytes.");
        }

        return new MemoryStream(_content, writable: false);
    }
}
