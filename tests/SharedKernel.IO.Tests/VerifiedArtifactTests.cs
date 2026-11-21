using System.Security.Cryptography;
using System.Text.Json;

namespace PandaBear.IO.Tests;

/// <summary>Exercises reviewed artifacts with an independent inventory manifest.</summary>
public sealed class VerifiedArtifactTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task A_manifest_digest_covers_the_original_bytes_and_artifacts_use_its_directory()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var inventoryPath = Path.Combine(_directory.Path, "inventory.json");
        byte[] payload = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(Path.Combine(_directory.Path, "map.bin"), payload, ct);
        var digest = Convert.ToHexStringLower(SHA256.HashData(payload));
        var json = $$""" { "name": "Museum", "path": "map.bin", "digest": "{{digest}}" } """;
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await File.WriteAllBytesAsync(inventoryPath, bytes, ct);

        // Act
        var (manifest, directory, manifestDigest) = await ArtifactManifest.Read(inventoryPath, 1024, ArtifactManifestJsonContext.Default.ArtifactInventory, ct);
        var artifact = await VerifiedArtifact.Read(directory, manifest.Path, manifest.Digest, 4, ct);

        // Assert
        manifest.Name.ShouldBe("Museum");
        manifestDigest.ShouldBe(Convert.ToHexString(SHA256.HashData(bytes)));
        artifact.ToArray().ShouldBe(payload);
    }

    [Theory]
    [InlineData("""{"name":"A","name":"B","path":"map.bin","digest":"d"}""")]
    [InlineData("""{"name":"A","path":"map.bin","digest":"d","unknown":true}""")]
    [InlineData("""{"name":"A","path":"map.bin"}""")]
    public async Task Manifest_deserialization_uses_the_callers_strict_contract(string json)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(_directory.Path, "inventory.json");
        await File.WriteAllTextAsync(path, json, ct);

        // Act
        Func<Task> read = () => ArtifactManifest.Read(path, 1024, ArtifactManifestJsonContext.Default.ArtifactInventory, ct);

        // Assert
        await read.ShouldThrow<JsonException>();
    }

    [Fact]
    public async Task Tampered_and_oversized_artifacts_cannot_be_read()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        byte[] bytes = [1, 2, 3];
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        await File.WriteAllBytesAsync(Path.Combine(_directory.Path, "map.bin"), bytes, ct);

        // Act
        Func<Task> tooLarge = () => VerifiedArtifact.Read(_directory.Path, "map.bin", digest, 2, ct);
        Func<Task> wrongDigest = () => VerifiedArtifact.Verify(_directory.Path, "map.bin", new string('0', 64), ct);

        // Assert
        await tooLarge.ShouldThrow<InvalidDataException>();
        await wrongDigest.ShouldThrow<InvalidDataException>();
    }
}
