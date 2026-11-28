using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using SharedKernel.IO;

namespace SharedKernel.Versioning;

/// <summary>Verifies one Portable PDB, its packaged PE, and a caller-selected source URL.</summary>
internal static class PortableSourceLink
{
    private const int AssetLimit = 32 * 1024 * 1024;
    private const int MemoryThreshold = 256 * 1024;
    private const int MappingLimit = 256 * 1024;
    private static readonly Guid SourceLinkKind = new("cc110556-a091-4d38-9fec-25ab9a351a6a");

    /// <summary>Validates a caller pattern and binds its revision segment to the candidate.</summary>
    /// <exception cref="ArgumentException">The requested Source Link URL pattern is invalid.</exception>
    internal static Uri Bind(string pattern, string revision)
    {
        const string placeholder = "/{revision}/";
        var index = pattern.IndexOf(placeholder, StringComparison.Ordinal);
        if (index < 0 || pattern.LastIndexOf(placeholder, StringComparison.Ordinal) != index
            || !pattern.EndsWith("/*", StringComparison.Ordinal) || pattern.IndexOf('*', StringComparison.Ordinal) != pattern.Length - 1
            || !TryHttps(pattern[..^1].Replace("{revision}", revision, StringComparison.Ordinal), out var prefix)
            || !prefix.AbsolutePath.Contains("/" + revision + "/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Source URL pattern must be HTTPS with one /{revision}/ segment and a trailing /*.");
        }

        return prefix;
    }

    /// <summary>Returns the bounded result for one exact assembly and symbol ZIP entry.</summary>
    internal static async Task<string> Check(ZipArchiveEntry assembly, ZipArchiveEntry symbols, Uri sourcePrefix, CancellationToken ct)
    {
        try
        {
            var assemblyStream = await assembly.OpenAsync(ct).ConfigureAwait(false);
            await using var assemblyStreamOwner = assemblyStream.ConfigureAwait(false);
            var symbolStream = await symbols.OpenAsync(ct).ConfigureAwait(false);
            await using var symbolStreamOwner = symbolStream.ConfigureAwait(false);
            var assemblyBytes = await SeekableContent.Create(assemblyStream, AssetLimit, MemoryThreshold, ct).ConfigureAwait(false);
            await using var assemblyBytesOwner = assemblyBytes.ConfigureAwait(false);
            var symbolBytes = await SeekableContent.Create(symbolStream, AssetLimit, MemoryThreshold, ct).ConfigureAwait(false);
            await using var symbolBytesOwner = symbolBytes.ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            using var image = new PEReader(assemblyBytes.Content, PEStreamOptions.LeaveOpen);
            using var provider = MetadataReaderProvider.FromPortablePdbStream(symbolBytes.Content, MetadataStreamOptions.LeaveOpen);
            var reader = provider.GetMetadataReader();
            if (!image.HasMetadata || reader.DebugMetadataHeader is not { } header || !Pairs(image, new BlobContentId(header.Id)))
            {
                return "assembly-mismatch";
            }

            return CheckMapping(reader, sourcePrefix, ct);
        }
        catch (JsonException)
        {
            return "invalid-source-link";
        }
        catch (Exception exception) when (exception is BadImageFormatException or InvalidDataException)
        {
            return "invalid-symbol";
        }
    }

    private static bool Pairs(PEReader image, BlobContentId symbolId)
    {
        foreach (var entry in image.ReadDebugDirectory())
        {
            if (entry.Type == DebugDirectoryEntryType.CodeView && entry.IsPortableCodeView
                && new BlobContentId(image.ReadCodeViewDebugDirectoryData(entry).Guid, entry.Stamp) == symbolId)
            {
                return true;
            }
        }

        return false;
    }

    private static string CheckMapping(MetadataReader reader, Uri sourcePrefix, CancellationToken ct)
    {
        BlobHandle value = default;
        foreach (var handle in reader.GetCustomDebugInformation(MetadataTokens.EntityHandle(TableIndex.Module, 1)))
        {
            var information = reader.GetCustomDebugInformation(handle);
            if (reader.GetGuid(information.Kind) != SourceLinkKind)
            {
                continue;
            }

            if (!value.IsNil || information.Value.IsNil)
            {
                return "invalid-source-link";
            }

            value = information.Value;
        }

        if (value.IsNil)
        {
            return "missing-source-link";
        }

        if (reader.GetBlobReader(value).Length > MappingLimit)
        {
            return "invalid-source-link";
        }

        using var json = JsonDocument.Parse(reader.GetBlobBytes(value), new JsonDocumentOptions { MaxDepth = 8 });
        if (json.RootElement.ValueKind != JsonValueKind.Object
            || !json.RootElement.TryGetProperty("documents", out var documents)
            || documents.ValueKind != JsonValueKind.Object)
        {
            return "invalid-source-link";
        }

        HashSet<string> patterns = new(StringComparer.OrdinalIgnoreCase);
        foreach (var property in documents.EnumerateObject())
        {
            ct.ThrowIfCancellationRequested();
            var wildcard = property.Name.IndexOf('*', StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(property.Name) || (wildcard >= 0 && wildcard != property.Name.Length - 1)
                || !patterns.Add(property.Name) || property.Value.ValueKind != JsonValueKind.String)
            {
                return "invalid-source-link";
            }

            var url = property.Value.GetString()!;
            if (url.IndexOf('*', StringComparison.Ordinal) != (wildcard >= 0 ? url.Length - 1 : -1)
                || !TryHttps(wildcard >= 0 && url.Length != 0 ? url[..^1] : url, out var source))
            {
                return "invalid-source-link";
            }

            if (source.IdnHost != sourcePrefix.IdnHost || source.Port != sourcePrefix.Port
                || !source.AbsolutePath.StartsWith(sourcePrefix.AbsolutePath, StringComparison.Ordinal))
            {
                return "source-link-mismatch";
            }
        }

        return patterns.Count == 0 ? "invalid-source-link" : "passed";
    }

    private static bool TryHttps(string value, [NotNullWhen(true)] out Uri? uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
