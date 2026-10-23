namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>Two items, named by origin key, that a match override links or splits.</summary>
/// <param name="First">Origin key of the first item.</param>
/// <param name="Second">Origin key of the second item.</param>
internal sealed record OriginKeyPair(string First, string Second);
