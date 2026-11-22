namespace PandaBear.ObjectStorage.FileSystem;

/// <summary>Application policy for the filesystem object store.</summary>
/// <param name="rootPath">The root directory available to the storage process.</param>
/// <param name="publicUri">The application mapping from a key to its public address.</param>
/// <param name="readMetadata">The application mapping for reads. The filesystem stores the content bytes.</param>
public sealed class FileSystemObjectStoreOptions(
    string rootPath,
    Func<ObjectStorageKey, Uri> publicUri,
    Func<ObjectStorageKey, (string ContentType, string? Checksum)> readMetadata)
{
    /// <summary>Gets the storage root.</summary>
    public string RootPath { get; } = rootPath;

    /// <summary>Gets the application's mapping from a key to its public address.</summary>
    public Func<ObjectStorageKey, Uri> PublicUri { get; } = publicUri;

    /// <summary>Gets the application's mapping for the content type and optional checksum returned on reads.</summary>
    public Func<ObjectStorageKey, (string ContentType, string? Checksum)> ReadMetadata { get; } = readMetadata;

    /// <summary>Gets the application limit for key and prefix length.</summary>
    public int MaxKeyLength { get; init; } = int.MaxValue;

    /// <summary>Gets the optional application policy for individual objects.</summary>
    public Func<ObjectStorageKey, bool>? KeyAllowed { get; init; }

    /// <summary>Gets the prefix used to identify temporary write files, including files left by a stopped process.</summary>
    public string TemporaryFilePrefix { get; init; } = ".objects-";
}
