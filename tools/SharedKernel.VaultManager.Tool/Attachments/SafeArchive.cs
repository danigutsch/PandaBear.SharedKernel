using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;

namespace PandaBear.VaultManager.Tool.Attachments;

/// <summary>
/// Zip archive that was fully validated before use: entry count, expanded size, per-entry compression ratio measured on
/// actual bytes, nesting depth, and entry paths. Declared sizes are never trusted.
/// </summary>
internal sealed class SafeArchive : IDisposable
{
    private const int EndOfCentralDirectoryLength = 22;
    private const int MaxCommentLength = ushort.MaxValue;
    private const ushort Zip64EntryCountMarker = ushort.MaxValue;

    private static readonly System.Text.UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ZipArchive archive;
    private readonly Dictionary<string, ArchiveEntryInfo> byPath;

    private SafeArchive(ZipArchive archive, IReadOnlyList<ArchiveEntryInfo> entries)
    {
        this.archive = archive;
        Entries = entries;
        byPath = entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
    }

    /// <summary>File entries in archive order. Directory entries are validated but not listed.</summary>
    public IReadOnlyList<ArchiveEntryInfo> Entries { get; }

    /// <summary>Opens and validates the archive at <paramref name="path"/> with the default limits.</summary>
    /// <param name="path">Zip file.</param>
    /// <returns>Validated archive.</returns>
    /// <exception cref="InvalidDataException">The file is not a zip archive or breaks a limit.</exception>
    public static SafeArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        FileStream stream = File.OpenRead(path);
        try
        {
            return Open(stream, ArchiveLimits.Default);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Opens and validates an archive. The archive takes ownership of <paramref name="stream"/>.</summary>
    /// <param name="stream">Seekable stream holding the zip archive.</param>
    /// <param name="limits">Bounds to enforce.</param>
    /// <returns>Validated archive.</returns>
    /// <exception cref="InvalidDataException">The data is not a zip archive or breaks a limit.</exception>
    public static SafeArchive Open(Stream stream, ArchiveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(limits);

        var budget = new Budget(limits);
        ZipArchive archive = OpenZip(stream, budget, depth: 0);
        try
        {
            var entries = new List<ArchiveEntryInfo>();
            Validate(archive, budget, depth: 0, entries);
            return new SafeArchive(archive, entries);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    /// <summary>Finds a file entry by its exact path.</summary>
    /// <param name="path">Entry path with <c>/</c> separators.</param>
    /// <returns>The entry, or <see langword="null"/> when the archive has no such file.</returns>
    public ArchiveEntryInfo? Find(string path) => byPath.GetValueOrDefault(path);

    /// <summary>Reads an entry's bytes, never more than the size measured during validation.</summary>
    /// <param name="entry">Entry of this archive.</param>
    /// <returns>Entry content.</returns>
    /// <exception cref="InvalidDataException">The entry changed size since validation.</exception>
    public byte[] ReadAllBytes(ArchiveEntryInfo entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var buffer = new MemoryStream();
        CopyEntry(entry, buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// Computes the SHA-256 of an entry's bytes while streaming them, never reading more than the size measured during
    /// validation, so a large binary file is not held in memory.
    /// </summary>
    /// <param name="entry">Entry of this archive.</param>
    /// <returns>The 32-byte digest.</returns>
    /// <exception cref="InvalidDataException">The entry changed size since validation.</exception>
    public byte[] ComputeSha256(ArchiveEntryInfo entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var sha256 = SHA256.Create();
        using (var hashing = new CryptoStream(Stream.Null, sha256, CryptoStreamMode.Write))
        {
            CopyEntry(entry, hashing);
        }

        return sha256.Hash!;
    }

    /// <summary>Reads an entry as strict UTF-8 text, for example an export document inside the archive.</summary>
    /// <param name="entry">Entry of this archive.</param>
    /// <returns>Entry text without a byte order mark.</returns>
    /// <exception cref="InvalidDataException">The entry is not valid UTF-8.</exception>
    public string ReadUtf8(ArchiveEntryInfo entry)
    {
        byte[] bytes = ReadAllBytes(entry);
        try
        {
            return StrictUtf8.GetString(bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? bytes.AsSpan(3) : bytes);
        }
        catch (System.Text.DecoderFallbackException exception)
        {
            throw new InvalidDataException("Archive entry is not valid UTF-8 text.", exception);
        }
    }

    /// <summary>Extracts an entry's text within the text extraction limits.</summary>
    /// <param name="entry">Entry of this archive.</param>
    /// <returns>Extracted text, or <see langword="null"/> when the entry is not a text kind. Binaries are never decoded.</returns>
    public ExtractedText? ReadText(ArchiveEntryInfo entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return AttachmentClassifier.IsText(entry.Kind) ? TextExtractor.Extract(ReadAllBytes(entry)) : null;
    }

    /// <inheritdoc/>
    public void Dispose() => archive.Dispose();

    private void CopyEntry(ArchiveEntryInfo entry, Stream destination)
    {
        ZipArchiveEntry zipEntry = archive.GetEntry(entry.Path)
            ?? throw new ArgumentException("Entry does not belong to this archive.", nameof(entry));
        using Stream source = zipEntry.Open();
        long read = CopyBounded(source, destination, entry.Size, () => "Archive entry grew after validation.");
        if (read != entry.Size)
        {
            throw new InvalidDataException("Archive entry changed size after validation.");
        }
    }

    private static ZipArchive OpenZip(Stream stream, Budget budget, int depth)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Archive stream must be seekable.", nameof(stream));
        }

        // ZipArchive loads the whole central directory up front; check the declared entry count first so a crafted
        // directory cannot exhaust memory. ZipArchive rejects archives whose directory disagrees with this count.
        int declared = ReadDeclaredEntryCount(stream);
        if (declared > budget.Limits.MaxEntries - budget.Entries)
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                $"Archive has more than {budget.Limits.MaxEntries} entries."));
        }

        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: depth > 0);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("File is not a valid zip archive.", exception);
        }
    }

    private static int ReadDeclaredEntryCount(Stream stream)
    {
        long length = stream.Length;
        if (length < EndOfCentralDirectoryLength)
        {
            throw new InvalidDataException("File is not a valid zip archive.");
        }

        int tailLength = (int)Math.Min(length, EndOfCentralDirectoryLength + MaxCommentLength);
        byte[] tail = new byte[tailLength];
        stream.Seek(length - tailLength, SeekOrigin.Begin);
        stream.ReadExactly(tail);
        stream.Seek(0, SeekOrigin.Begin);

        for (int offset = tailLength - EndOfCentralDirectoryLength; offset >= 0; offset--)
        {
            if (tail[offset] == 'P' && tail[offset + 1] == 'K' && tail[offset + 2] == 5 && tail[offset + 3] == 6)
            {
                ushort count = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(offset + 10, 2));

                // Zip64 archives store the real count elsewhere; no vault export needs that many entries.
                return count == Zip64EntryCountMarker ? int.MaxValue : count;
            }
        }

        throw new InvalidDataException("File is not a valid zip archive.");
    }

    private static void Validate(ZipArchive archive, Budget budget, int depth, List<ArchiveEntryInfo>? entries)
    {
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            int index = budget.Entries;
            budget.Entries++;
            if (budget.Entries > budget.Limits.MaxEntries)
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                    $"Archive has more than {budget.Limits.MaxEntries} entries."));
            }

            string path = entry.FullName;
            if (!IsSafePath(path))
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                    $"Archive entry {index} has an unsafe path that could escape the archive."));
            }

            if (path.EndsWith('/') && entry.Length == 0)
            {
                continue;
            }

            (long size, byte[] head, MemoryStream? nested) = Measure(entry, index, budget);
            using (nested)
            {
                AttachmentKind kind = AttachmentClassifier.Classify(head, complete: size == head.Length, entry.Name);
                if (kind == AttachmentKind.Zip && nested is not null)
                {
                    ValidateNested(nested, budget, depth + 1);
                }

                entries?.Add(new ArchiveEntryInfo(path, size, kind));
            }
        }
    }

    private static void ValidateNested(MemoryStream nested, Budget budget, int depth)
    {
        if (depth > budget.Limits.MaxNestingDepth)
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                $"Archive is nested more than {budget.Limits.MaxNestingDepth} levels deep."));
        }

        nested.Position = 0;
        using ZipArchive inner = OpenZip(nested, budget, depth);
        Validate(inner, budget, depth, entries: null);
    }

    private static (long Size, byte[] Head, MemoryStream? Nested) Measure(ZipArchiveEntry entry, int index, Budget budget)
    {
        ArchiveLimits limits = budget.Limits;
        long ratioLimit = Math.Max(limits.RatioExemptBytes, entry.CompressedLength * limits.MaxCompressionRatio);
        long totalLimit = limits.MaxExpandedBytes - budget.ExpandedBytes;
        long limit = Math.Min(ratioLimit, totalLimit);

        // Keep the whole entry only when it may be a nested archive; everything else streams through.
        using Stream source = entry.Open();
        byte[] head = new byte[AttachmentClassifier.HeadLength];
        int headLength = source.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        MemoryStream? nested = head.AsSpan(0, headLength).StartsWith("PK\x03\x04"u8) ? new MemoryStream() : null;
        try
        {
            nested?.Write(head, 0, headLength);
            if (headLength > limit)
            {
                throw LimitExceeded(index, ratioLimit < totalLimit, limits);
            }

            long size = headLength + CopyBounded(source, nested ?? Stream.Null, limit - headLength,
                () => LimitExceeded(index, ratioLimit < totalLimit, limits).Message);
            budget.ExpandedBytes += size;
            return (size, head[..headLength], nested);
        }
        catch
        {
            nested?.Dispose();
            throw;
        }
    }

    private static InvalidDataException LimitExceeded(int index, bool ratio, ArchiveLimits limits) =>
        new(ratio
            ? string.Create(CultureInfo.InvariantCulture,
                $"Archive entry {index} expands beyond a compression ratio of {limits.MaxCompressionRatio}:1.")
            : string.Create(CultureInfo.InvariantCulture,
                $"Archive expanded size exceeds {limits.MaxExpandedBytes} bytes."));

    private static long CopyBounded(Stream source, Stream destination, long limit, Func<string> overLimitMessage)
    {
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer)) > 0)
        {
            total += read;
            if (total > limit)
            {
                throw new InvalidDataException(overLimitMessage());
            }

            destination.Write(buffer, 0, read);
        }

        return total;
    }

    private static bool IsSafePath(string path)
    {
        if (path.Length == 0 || path.Contains('\0', StringComparison.Ordinal) || path[0] is '/' or '\\')
        {
            return false;
        }

        if (path.Length >= 2 && path[1] == ':' && char.IsAsciiLetter(path[0]))
        {
            return false;
        }

        return !path.Split('/', '\\').Contains("..", StringComparer.Ordinal);
    }

    private sealed class Budget(ArchiveLimits limits)
    {
        public ArchiveLimits Limits { get; } = limits;

        public int Entries { get; set; }

        public long ExpandedBytes { get; set; }
    }
}
