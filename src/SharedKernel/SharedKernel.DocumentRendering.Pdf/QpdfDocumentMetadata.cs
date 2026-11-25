using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SharedKernel.DocumentRendering.Pdf;

/// <summary>Edits the qpdf JSON envelope for the qualified Chromium Info dictionary.</summary>
internal static class QpdfDocumentMetadata
{
    /// <summary>Reads the two selected object references from the trailer of a fresh generated document.</summary>
    /// <exception cref="InvalidDataException">The trailer or its qpdf JSON envelope is unsupported.</exception>
    public static (string Info, string Catalog) References(ReadOnlyMemory<byte> json)
    {
        var (_, objects) = Read(json);
        var trailer = Dictionary(objects, "trailer");
        if (trailer.ContainsKey("/Encrypt") || trailer.ContainsKey("/ID"))
        {
            throw UnsupportedMetadata();
        }

        return (Reference(trailer["/Info"]), Reference(trailer["/Root"]));
    }

    /// <summary>Turns an already validated reference into a qpdf selector.</summary>
    public static string SelectObject(string reference) => $"--json-object={reference[..^2].Replace(' ', ',')}";

    /// <summary>Writes an update for the Info object only, preserving its remaining members.</summary>
    /// <exception cref="InvalidDataException">The qpdf JSON does not describe the qualified Chromium metadata.</exception>
    public static string UpdateDates(ReadOnlyMemory<byte> json, string infoReference, string catalogReference, DateTimeOffset documentDate)
    {
        var (header, objects) = Read(json);
        var infoKey = $"obj:{infoReference}";
        var info = Dictionary(objects, infoKey);
        var catalog = Dictionary(objects, $"obj:{catalogReference}");
        if (catalog.ContainsKey("/Metadata")
            || catalog["/MarkInfo"] is not JsonObject mark
            || mark["/Marked"] is not JsonValue marked
            || !marked.TryGetValue<bool>(out var tagged) || !tagged
            || !HasString(catalog["/Lang"], "u:")
            || !HasString(info["/Title"], "u:")
            || ReadString(info["/Creator"]) != "u:Chromium"
            || !HasString(info["/Producer"], "u:Skia/PDF ")
            || !HasString(info["/CreationDate"], "u:D:")
            || !HasString(info["/ModDate"], "u:D:"))
        {
            throw UnsupportedMetadata();
        }

        _ = Reference(catalog["/StructTreeRoot"]);
        var date = $"u:D:{documentDate.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}+00'00'";
        info["/CreationDate"] = date;
        info["/ModDate"] = date;
        return new JsonObject
        {
            ["qpdf"] = new JsonArray(header.DeepClone(), new JsonObject { [infoKey] = objects[infoKey]!.DeepClone() }),
        }.ToJsonString();
    }

    private static (JsonObject Header, JsonObject Objects) Read(ReadOnlyMemory<byte> json)
    {
        try
        {
            if (JsonNode.Parse(json.Span) is JsonObject root
                && root["qpdf"] is JsonArray envelope && envelope is [JsonObject header, JsonObject objects]
                && header["jsonversion"] is JsonValue version && version.TryGetValue<int>(out var number) && number == 2)
            {
                return (header, objects);
            }
        }
        catch (JsonException)
        {
            // qpdf output can contain document metadata, so omit the parser's detail.
        }

        throw UnsupportedMetadata();
    }

    private static JsonObject Dictionary(JsonObject objects, string key) =>
        objects[key] is JsonObject entry && entry["value"] is JsonObject dictionary
            ? dictionary
            : throw UnsupportedMetadata();

    private static string Reference(JsonNode? node)
    {
        var reference = ReadString(node);
        if (reference?.Split(' ') is [var objectId, var generation, "R"]
            && int.TryParse(objectId, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            && ushort.TryParse(generation, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return reference;
        }

        throw UnsupportedMetadata();
    }

    private static bool HasString(JsonNode? node, string prefix) =>
        ReadString(node) is { } value && value.Length > prefix.Length && value.StartsWith(prefix, StringComparison.Ordinal);

    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static InvalidDataException UnsupportedMetadata() =>
        new("qpdf did not return supported metadata for a fresh Chromium tagged PDF.");
}
