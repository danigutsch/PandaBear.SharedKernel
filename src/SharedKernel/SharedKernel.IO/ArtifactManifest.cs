// Adapted from ViajantesTurismo under its MIT license. See LICENSE.ViajantesTurismo.txt.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PandaBear.IO;

/// <summary>Reads the caller's typed manifest and records the digest of the exact bytes read.</summary>
public static class ArtifactManifest
{
    /// <summary>Reads a bounded JSON manifest using caller-supplied serializer metadata.</summary>
    /// <typeparam name="T">The consumer's manifest type.</typeparam>
    /// <param name="path">The manifest file.</param>
    /// <param name="maxBytes">The largest accepted manifest size.</param>
    /// <param name="typeInfo">The JSON contract, including the consumer's strictness options.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The manifest, its containing directory, and uppercase SHA-256 of its exact file bytes.</returns>
    /// <exception cref="ArgumentException">The path is invalid.</exception>
    /// <exception cref="ArgumentNullException">The JSON contract is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size limit is negative.</exception>
    /// <exception cref="InvalidDataException">The manifest exceeds the size limit or deserializes to null.</exception>
    /// <exception cref="JsonException">The manifest violates the supplied JSON contract.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Access is denied.</exception>
    /// <exception cref="OperationCanceledException">The read was canceled.</exception>
    public static async Task<(T Manifest, string Directory, string Digest)> Read<T>(
        string path, int maxBytes, JsonTypeInfo<T> typeInfo, CancellationToken ct)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var manifestPath = Path.GetFullPath(path);
        var bytes = await BoundedReader.ReadFile(manifestPath, maxBytes, ct).ConfigureAwait(false)
            ?? throw new InvalidDataException("Migration manifest exceeds the size limit.");
        var manifest = JsonSerializer.Deserialize(bytes.AsSpan(), typeInfo)
            ?? throw new InvalidDataException("A governed migration JSON artifact must be an object.");
        var directory = Path.GetDirectoryName(manifestPath) ?? throw new InvalidDataException("A manifest directory is required.");
        return (manifest, directory, Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
