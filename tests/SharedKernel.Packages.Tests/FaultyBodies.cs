using System.IO.Compression;
using System.Net;

namespace PandaBear.Packages.Tests;

/// <summary>Response bodies that fail while they are read.</summary>
internal static class FaultyBodies
{
    /// <summary>A body that sends a few bytes and then fails like a reset connection.</summary>
    public static HttpResponseMessage ConnectionReset() =>
        new(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ResettingStream())
        };

    /// <summary>A gzip body with a valid header and corrupt data, which a decompressing stream rejects with <see cref="InvalidDataException"/>.</summary>
    public static HttpResponseMessage CorruptGzip()
    {
        byte[] bytes = [0x1f, 0x8b, 0x08, 0, 0, 0, 0, 0, 0, 0, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff];
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress)) };
    }
}
