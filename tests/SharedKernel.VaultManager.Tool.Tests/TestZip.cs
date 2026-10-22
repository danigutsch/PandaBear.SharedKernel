using System.IO.Compression;

namespace PandaBear.VaultManager.Tool.Tests;

internal static class TestZip
{
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

    public static byte[] RandomBytes(int length) => System.Security.Cryptography.RandomNumberGenerator.GetBytes(length);

    public static byte[] Utf8(string text) => System.Text.Encoding.UTF8.GetBytes(text);
}
