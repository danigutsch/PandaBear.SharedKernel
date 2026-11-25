using System.Buffers;
using System.Security.Cryptography;

namespace SharedKernel.IO;

/// <summary>
/// Stores files in one directory under the lowercase hexadecimal SHA-256 of their content plus an extension, so a name always
/// holds the same bytes and a file is written only once.
/// </summary>
/// <remarks>
/// Writes go through <see cref="AtomicFile.CreateIfAbsent"/>: a reader never sees half a file, and concurrent stores of the same
/// content publish it once. A file already present under the name is kept as it is and not compared. The caller owns the
/// directory, any record of the names, and removing a file that no record uses.
/// </remarks>
public sealed class ContentAddressedFileStore
{
    private const string AsciiLettersAndDigits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static readonly SearchValues<char> ExtensionCharacters = SearchValues.Create(AsciiLettersAndDigits);

    private static readonly SearchValues<char> NameCharacters = SearchValues.Create(AsciiLettersAndDigits + ".-_");

    private readonly string root;

    /// <summary>Creates a store over a directory, which is created on the first store.</summary>
    /// <param name="directory">The directory, absolute or relative to the current directory.</param>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is <see langword="null"/>, empty or white space.</exception>
    public ContentAddressedFileStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        root = Path.GetFullPath(directory);
    }

    /// <summary>The name that content is stored under: its lowercase hexadecimal SHA-256 followed by the extension.</summary>
    /// <param name="content">The content.</param>
    /// <param name="extension">Empty, or a dot followed by one or more ASCII letters or digits, such as <c>.webp</c>.</param>
    /// <returns>The file name.</returns>
    /// <exception cref="ArgumentException"><paramref name="extension"/> is not empty or a dot and ASCII letters or digits.</exception>
    public static string NameOf(ReadOnlySpan<byte> content, string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        if (extension.Length != 0 && (extension.Length == 1 || extension[0] != '.' || extension.AsSpan(1).ContainsAnyExcept(ExtensionCharacters)))
        {
            throw new ArgumentException("The extension must be empty or a dot followed by ASCII letters or digits.", nameof(extension));
        }

        return Convert.ToHexStringLower(SHA256.HashData(content)) + extension;
    }

    /// <summary>Writes content under <see cref="NameOf"/> unless a file of that name exists.</summary>
    /// <param name="content">The content.</param>
    /// <param name="extension">Empty, or a dot followed by one or more ASCII letters or digits, such as <c>.webp</c>.</param>
    /// <param name="ct">Cancellation observed during the write and before publication.</param>
    /// <returns>The name, and whether this call created the file.</returns>
    /// <exception cref="ArgumentException"><paramref name="extension"/> is not empty or a dot and ASCII letters or digits.</exception>
    /// <exception cref="IOException">The file cannot be written, or something other than a file occupies its name.</exception>
    /// <exception cref="UnauthorizedAccessException">The directory cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The store was canceled before publication.</exception>
    public async ValueTask<StoredFile> Store(ReadOnlyMemory<byte> content, string extension, CancellationToken ct)
    {
        var name = NameOf(content.Span, extension);
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(root);
        var created = await AtomicFile.CreateIfAbsent(
            Path.Combine(root, name),
            (stream, token) => stream.WriteAsync(content, token),
            ct).ConfigureAwait(false);
        return new StoredFile(name, created);
    }

    /// <summary>The path of a file in the store, such as one to serve or read.</summary>
    /// <param name="name">
    /// A file name of ASCII letters, digits, dots, hyphens and underscores that does not start with a dot, such as a name from
    /// <see cref="Store"/>.
    /// </param>
    /// <returns>The absolute path in the store directory.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not such a file name, so it could reach outside the directory.</exception>
    public string PathOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || name[0] == '.' || name.AsSpan().ContainsAnyExcept(NameCharacters))
        {
            throw new ArgumentException("The name must be ASCII letters, digits, dots, hyphens or underscores and must not start with a dot.", nameof(name));
        }

        return Path.Combine(root, name);
    }

    /// <summary>Deletes a file from the store; a missing file is not an error.</summary>
    /// <param name="name">The file name, as <see cref="PathOf"/> accepts it.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a plain file name.</exception>
    /// <exception cref="IOException">The file is in use.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be deleted.</exception>
    public void Delete(string name) => File.Delete(PathOf(name));
}
