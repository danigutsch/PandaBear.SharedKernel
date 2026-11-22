using PandaBear.IO;

namespace PandaBear.ObjectStorage.FileSystem;

/// <summary>Stores objects under a caller-owned directory and publishes their bytes atomically.</summary>
/// <remarks>The application owns the directory, key policy, metadata, and public addresses. Use a storage directory
/// writable only by trusted processes. This adapter is for object reads and writes through the local filesystem.</remarks>
public sealed class FileSystemObjectStore : IObjectStore
{
    private const string TemporaryFileSuffix = ".tmp";
    private const int UniqueNameLength = 32;
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly FileSystemObjectStoreOptions options;
    private readonly string root;

    /// <summary>Creates a store using the supplied policy.</summary>
    /// <param name="options">Filesystem and application policy.</param>
    /// <exception cref="ArgumentNullException">The options or either mapping is null.</exception>
    /// <exception cref="ArgumentException">The root or temporary-file prefix is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The key length limit is less than one.</exception>
    public FileSystemObjectStore(FileSystemObjectStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.PublicUri);
        ArgumentNullException.ThrowIfNull(options.ReadMetadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RootPath);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxKeyLength, 1);
        if (string.IsNullOrEmpty(options.TemporaryFilePrefix)
            || !options.TemporaryFilePrefix.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'))
        {
            throw new ArgumentException("The temporary file prefix must contain only ASCII letters, digits, dots, hyphens, or underscores.", nameof(options));
        }

        this.options = options;
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.RootPath));
    }

    /// <summary>
    /// Rewinds seekable content, copies the exact declared length, and verifies an optional SHA-256 checksum before publishing.
    /// The caller retains ownership of the input stream. Metadata stays with the application.
    /// </summary>
    /// <exception cref="ArgumentNullException">The request or its stream is null.</exception>
    /// <exception cref="ArgumentException">The key or checksum is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The length is negative.</exception>
    /// <exception cref="InvalidDataException">The length or checksum differs from the contents.</exception>
    /// <exception cref="InvalidOperationException">The path contains a symbolic link or the address mapping returns null.</exception>
    /// <exception cref="IOException">The input cannot be read or the file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The path denies access.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled before publication.</exception>
    public async ValueTask<ObjectWriteResult> Put(ObjectWriteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);
        ct.ThrowIfCancellationRequested();
        var key = ParseKey(request.ObjectKey);
        var path = ResolvePath(key);
        var publicUri = PublicUri(key);
        var expectedHash = VerifiedObjectContent.ParseChecksum(request.Checksum);
        if (request.Length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Length, "Object length must not be negative.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await AtomicFile.Replace(path, options.TemporaryFilePrefix,
            (destination, token) => VerifiedObjectContent.Copy(request.Content, destination, request.Length, expectedHash, token),
            ct).ConfigureAwait(false);
        return new ObjectWriteResult(request.ObjectKey, new Uri(path), publicUri, request.ContentType, request.Length, request.Checksum);
    }

    /// <summary>Opens a read snapshot. The caller disposes the returned result.</summary>
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    /// <exception cref="InvalidOperationException">The path contains a symbolic link.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">The object's directory does not exist.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The path denies access.</exception>
    /// <exception cref="OperationCanceledException">The token was canceled.</exception>
    public async ValueTask<ObjectReadResult> OpenRead(string objectKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = ParseKey(objectKey);
        var path = ResolvePath(key);
        var (contentType, checksum) = options.ReadMetadata(key);
        var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        });

        try
        {
            return new ObjectReadResult(objectKey, stream, contentType, stream.Length, checksum);
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    /// <exception cref="InvalidOperationException">The path contains a symbolic link.</exception>
    /// <exception cref="OperationCanceledException">The token was canceled.</exception>
    public ValueTask<bool> Exists(string objectKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(File.Exists(ResolvePath(ParseKey(objectKey))));
    }

    /// <summary>Lists files in ordinal key order, excluding symbolic links and reserved temporary files.</summary>
    /// <exception cref="ArgumentException">The prefix is invalid.</exception>
    /// <exception cref="OperationCanceledException">The listing was canceled.</exception>
    public ValueTask<IReadOnlyList<string>> ListKeys(string prefix, CancellationToken ct) =>
        ValueTask.FromResult<IReadOnlyList<string>>([.. Files(prefix, ct).Select(item => item.Key)]);

    /// <summary>Lists files as observed with their last modification time and in ordinal key order.</summary>
    /// <exception cref="ArgumentException">The prefix is invalid.</exception>
    /// <exception cref="OperationCanceledException">The listing was canceled.</exception>
    public ValueTask<IReadOnlyList<ObjectInventoryItem>> ListObjects(string prefix, CancellationToken ct) =>
        ValueTask.FromResult<IReadOnlyList<ObjectInventoryItem>>(
            [.. Files(prefix, ct).Select(item => new ObjectInventoryItem(item.Key, new DateTimeOffset(item.File.LastWriteTimeUtc)))]);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    /// <exception cref="InvalidOperationException">The address mapping returned null.</exception>
    public Uri GetPublicUri(string objectKey) => PublicUri(ParseKey(objectKey));

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">The filesystem does not issue direct upload tickets.</exception>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="OperationCanceledException">The token was canceled.</exception>
    public ValueTask<ObjectUploadTicket> CreateUploadUrl(ObjectUploadRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        throw new NotSupportedException("Filesystem object storage does not support direct upload tickets.");
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    /// <exception cref="InvalidOperationException">The path contains a symbolic link.</exception>
    /// <exception cref="IOException">The object cannot be deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">The path denies access.</exception>
    /// <exception cref="OperationCanceledException">The token was canceled.</exception>
    public ValueTask Delete(string objectKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(ParseKey(objectKey));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return ValueTask.CompletedTask;
    }

    private Uri PublicUri(ObjectStorageKey key) => options.PublicUri(key)
        ?? throw new InvalidOperationException("The public address mapping must return a URI.");

    private ObjectStorageKey ParseKey(string objectKey)
    {
        if (!ObjectStorageKey.TryFrom(objectKey, out var key, options.MaxKeyLength)
            || key.Value.Split('/').Any(segment => !IsFileName(segment))
            || IsTemporary(Path.GetFileName(key.Value))
            || options.KeyAllowed?.Invoke(key) == false)
        {
            throw new ArgumentException("The object key is invalid for the filesystem store.", nameof(objectKey));
        }

        return key;
    }

    private static bool IsFileName(string segment)
    {
        if (segment.IndexOfAny(InvalidFileNameChars) >= 0)
        {
            return false;
        }

        return !OperatingSystem.IsWindows() || (segment[^1] is not (' ' or '.') && !IsWindowsDevice(segment));
    }

    private static bool IsWindowsDevice(string segment)
    {
        var stem = segment.Split('.', 2)[0].TrimEnd().ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                && stem[3] is >= '1' and <= '9' or '¹' or '²' or '³');
    }

    private string ResolvePath(ObjectStorageKey key)
    {
        var current = root;
        foreach (var segment in key.Value.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (new FileInfo(current).LinkTarget is not null || new DirectoryInfo(current).LinkTarget is not null)
            {
                throw new InvalidOperationException("Symbolic links are not followed in object paths.");
            }
        }

        return current;
    }

    private IEnumerable<(string Key, FileInfo File)> Files(string prefix, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var selectedPrefix = ObjectStoragePrefix.From(prefix, options.MaxKeyLength).Value;
        var folder = new DirectoryInfo(root);
        if (!folder.Exists)
        {
            return [];
        }

        return ObserveFiles(folder, selectedPrefix, ct).OrderBy(item => item.Key, StringComparer.Ordinal);
    }

    private IEnumerable<(string Key, FileInfo File)> ObserveFiles(DirectoryInfo folder, string prefix, CancellationToken ct)
    {
        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
        };
        foreach (var file in folder.EnumerateFiles("*", enumeration))
        {
            ct.ThrowIfCancellationRequested();
            if (!IsTemporary(file.Name))
            {
                var key = Path.GetRelativePath(root, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    yield return (key, file);
                }
            }
        }
    }

    private bool IsTemporary(string fileName)
    {
        var prefix = options.TemporaryFilePrefix;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return fileName.Length == prefix.Length + UniqueNameLength + TemporaryFileSuffix.Length
            && fileName.StartsWith(prefix, comparison)
            && fileName.EndsWith(TemporaryFileSuffix, comparison)
            && Guid.TryParseExact(fileName.AsSpan(prefix.Length, UniqueNameLength), "N", out _);
    }
}
