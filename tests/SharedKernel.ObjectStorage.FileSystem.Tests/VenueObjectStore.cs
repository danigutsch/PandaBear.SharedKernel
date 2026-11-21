namespace PandaBear.ObjectStorage.FileSystem.Tests;

/// <summary>Configures a fictional community venue poster store for compatibility tests.</summary>
internal static class VenueObjectStore
{
    /// <summary>Creates a venue store in the caller-owned directory.</summary>
    internal static FileSystemObjectStore Create(string root) => new(new FileSystemObjectStoreOptions(
        root,
        key => new Uri("/venue/" + key.ToEscapedPath(), UriKind.Relative),
        _ => ("image/webp", null))
    {
        MaxKeyLength = 1024,
        KeyAllowed = key => key.Value.StartsWith("posters/", StringComparison.Ordinal),
    });
}
