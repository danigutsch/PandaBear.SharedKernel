using System.Reflection;
using System.Security.Cryptography;

namespace Aspire.Hosting;

/// <summary>
/// Writes the embedded k6 helper library to a content-addressed directory under the per-user data folder that a
/// container can mount.
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
        var userData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(userData))
        {
            throw new InvalidOperationException("The per-user data folder is not available to hold the k6 library.");
        }

        var directory = Path.Combine(userData, "sharedkernel-k6", hash);
        Directory.CreateDirectory(directory);
        if (new DirectoryInfo(directory).LinkTarget is not null)
        {
            throw new InvalidOperationException($"The k6 library directory '{directory}' must not be a link.");
        }

        foreach (var (name, content) in files)
        {
            var path = Path.Combine(directory, name);
            var info = new FileInfo(path);
            if (info.LinkTarget is not null || !info.Exists || !File.ReadAllBytes(path).AsSpan().SequenceEqual(content))
            {
                // Replace the path itself, so an existing link is removed instead of written through.
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(temporary, content);
                File.Move(temporary, path, overwrite: true);
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
