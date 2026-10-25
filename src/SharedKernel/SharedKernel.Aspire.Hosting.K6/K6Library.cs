using System.Reflection;
using System.Security.Cryptography;

namespace Aspire.Hosting;

/// <summary>
/// Writes the embedded k6 helper library to a content-addressed directory that a container can mount.
/// </summary>
internal static class K6Library
{
    private const string ResourcePrefix = "k6lib/";

    internal static string Materialize()
    {
        var assembly = typeof(K6Library).Assembly;
        var files = assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => (Name: name[ResourcePrefix.Length..], Content: Read(assembly, name)))
            .ToArray();

        var hash = Convert.ToHexString(SHA256.HashData(files.SelectMany(static file => file.Content).ToArray()))[..16];
        var directory = Path.Combine(Path.GetTempPath(), "sharedkernel-k6-" + hash);
        Directory.CreateDirectory(directory);

        foreach (var (name, content) in files)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(content))
            {
                File.WriteAllBytes(path, content);
            }
        }

        return directory;
    }

    private static byte[] Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
