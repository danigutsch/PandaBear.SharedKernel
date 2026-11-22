namespace PandaBear.IO.Tests;

/// <summary>A fictional inventory item backed by a reviewed file.</summary>
/// <param name="Name">The exhibit name.</param>
/// <param name="Path">The relative file.</param>
/// <param name="Digest">The file SHA-256.</param>
internal sealed record ArtifactInventory(string Name, string Path, string Digest);
