using System.Text;
using System.Text.Json.Nodes;

namespace SharedKernel.DocumentRendering.Pdf;

/// <summary>Prepares isolated pagination streams for qualified renderer-owned A4 pages.</summary>
internal sealed class QpdfPageNumbering
{
    private const int MaxPages = 128;
    private const int MaxPageStreams = 16;
    private const decimal A4Width = 595m;
    private const decimal A4Height = 842m;
    private const decimal GeometryTolerance = 1m;

    private readonly (string Reference, JsonArray Contents)[] _pages;

    private QpdfPageNumbering((string Reference, JsonArray Contents)[] pages)
    {
        _pages = pages;
    }

    /// <summary>Reads the ordered Chromium pages and their original content references.</summary>
    /// <exception cref="InvalidDataException">The page list does not match the bounded Chromium contract.</exception>
    public static QpdfPageNumbering Read(ReadOnlyMemory<byte> json)
    {
        var (root, header, _) = QpdfDocumentMetadata.Read(json);
        if (QpdfDocumentMetadata.ReadString(header["pdfversion"]) != "1.4"
            || root["pages"] is not JsonArray pages
            || pages.Count is 0 or > MaxPages)
        {
            throw UnsupportedPageMetadata();
        }

        var orderedPages = new (string Reference, JsonArray Contents)[pages.Count];
        var references = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < pages.Count; index++)
        {
            if (pages[index] is not JsonObject page
                || page["pageposfrom1"] is not JsonValue position
                || !position.TryGetValue<int>(out var number) || number != index + 1
                || page["contents"] is not JsonArray contents
                || contents.Count is 0 or > MaxPageStreams)
            {
                throw UnsupportedPageMetadata();
            }

            var reference = QpdfDocumentMetadata.Reference(page["object"]);
            if (!references.Add(reference))
            {
                throw UnsupportedPageMetadata();
            }

            foreach (var content in contents)
            {
                _ = QpdfDocumentMetadata.Reference(content);
            }

            orderedPages[index] = (reference, contents);
        }

        return new QpdfPageNumbering(orderedPages);
    }

    /// <summary>Selects only the page dictionaries that require a pagination update.</summary>
    public string[] Selectors() =>
        _pages.Select(static page => QpdfDocumentMetadata.SelectObject(page.Reference)).ToArray();

    /// <summary>Combines the date update and page prepends in one selective qpdf JSON update.</summary>
    /// <exception cref="InvalidDataException">The selected objects do not match the qualified page list or geometry.</exception>
    /// <exception cref="IOException">A pagination stream could not be written to the private working directory.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public async Task<string> WriteUpdate(
        string workingDirectory,
        ReadOnlyMemory<byte> metadata,
        string infoReference,
        string catalogReference,
        DateTimeOffset documentDate,
        CancellationToken ct)
    {
        var (_, header, objects) = QpdfDocumentMetadata.Read(metadata);
        if (QpdfDocumentMetadata.ReadString(header["pdfversion"]) != "1.4"
            || header["maxobjectid"] is not JsonValue maximum
            || !maximum.TryGetValue<int>(out var maxObjectId)
            || maxObjectId <= 0 || maxObjectId > int.MaxValue - _pages.Length)
        {
            throw UnsupportedPageMetadata();
        }

        var updates = QpdfDocumentMetadata.DateUpdates(objects, infoReference, catalogReference, documentDate);
        for (var index = 0; index < _pages.Length; index++)
        {
            ct.ThrowIfCancellationRequested();
            var (pageReference, listedContents) = _pages[index];
            var key = $"obj:{pageReference}";
            if (objects[key] is not JsonObject entry || entry["value"] is not JsonObject page)
            {
                throw UnsupportedPageMetadata();
            }

            var width = PageWidth(page);
            var contents = page["/Contents"] switch
            {
                JsonValue original => new JsonArray(original.DeepClone()),
                JsonArray original => (JsonArray)original.DeepClone(),
                _ => throw UnsupportedPageMetadata(),
            };
            if (!JsonNode.DeepEquals(contents, listedContents))
            {
                throw UnsupportedPageMetadata();
            }

            var number = index + 1;
            var artifactReference = FormattableString.Invariant($"{maxObjectId + number} 0 R");
            contents.Insert(0, JsonValue.Create(artifactReference));
            page["/Contents"] = contents;
            updates.Add(key, entry.DeepClone());

            var streamName = FormattableString.Invariant($"page-number-{number}.stream");
            var mark = QpdfPageNumberMark.Write(number, width);
            await File.WriteAllTextAsync(Path.Combine(workingDirectory, streamName), mark, Encoding.ASCII, ct).ConfigureAwait(false);
            updates.Add($"obj:{artifactReference}", new JsonObject
            {
                ["stream"] = new JsonObject
                {
                    ["dict"] = new JsonObject(),
                    ["datafile"] = streamName,
                },
            });
        }

        return QpdfDocumentMetadata.SerializeUpdate(header, updates);
    }

    private static decimal PageWidth(JsonObject page)
    {
        if (QpdfDocumentMetadata.ReadString(page["/Type"]) != "/Page"
            || page.ContainsKey("/Rotate") || page.ContainsKey("/CropBox") || page.ContainsKey("/UserUnit")
            || page["/MediaBox"] is not JsonArray box
            || box is not [JsonValue left, JsonValue bottom, JsonValue right, JsonValue top]
            || !left.TryGetValue<decimal>(out var x) || x != 0
            || !bottom.TryGetValue<decimal>(out var y) || y != 0
            || !right.TryGetValue<decimal>(out var width) || width is < A4Width - GeometryTolerance or > A4Width + GeometryTolerance
            || !top.TryGetValue<decimal>(out var height) || height is < A4Height - GeometryTolerance or > A4Height + GeometryTolerance)
        {
            throw UnsupportedPageMetadata();
        }

        return width;
    }

    private static InvalidDataException UnsupportedPageMetadata() =>
        new("qpdf did not return supported page metadata for a fresh Chromium A4 PDF.");
}
