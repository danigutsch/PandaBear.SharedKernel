using System.IO.Compression;

namespace PandaBear.VaultManager.Tool.Tests;

/// <summary>Builds in-memory zip archives and byte payloads for tests.</summary>
internal static class TestZip
{
    /// <summary>Creates a zip archive holding one smallest-size entry per path and content pair.</summary>
    public static byte[] Create(params (string Path, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string path, byte[] content) in entries)
            {
                var entry = archive.CreateEntry(path, CompressionLevel.SmallestSize);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Returns <paramref name="length"/> cryptographically random bytes.</summary>
    public static byte[] RandomBytes(int length) => System.Security.Cryptography.RandomNumberGenerator.GetBytes(length);

    /// <summary>Returns the UTF-8 bytes of <paramref name="text"/>.</summary>
    public static byte[] Utf8(string text) => System.Text.Encoding.UTF8.GetBytes(text);
}
