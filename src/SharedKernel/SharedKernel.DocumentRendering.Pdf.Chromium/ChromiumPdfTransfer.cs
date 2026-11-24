using System.Buffers;
using System.Text.Json;
using Microsoft.Playwright;

namespace PandaBear.DocumentRendering.Pdf.Chromium;

/// <summary>Reads a tagged Chromium PDF through the bounded DevTools stream.</summary>
internal static class ChromiumPdfTransfer
{
    private const int ChunkLimit = 8_192;
    private const string InvalidStream = "Chromium did not return an exclusive PDF stream.";
    private const string InvalidChunk = "Chromium returned an invalid bounded PDF chunk.";

    /// <summary>Prints a document in an owned context and reads at most one byte beyond its limit.</summary>
    /// <exception cref="InvalidDataException">Chromium returns an unsupported stream shape or exceeds the limit.</exception>
    internal static async Task<ReadOnlyMemory<byte>> Print(ICDPSession session, int limit, CancellationToken ct)
    {
        var reply = RequireResult(await session.SendAsync("Page.printToPDF", new Dictionary<string, object>
        {
            ["transferMode"] = "ReturnAsStream",
            ["generateTaggedPDF"] = true,
            ["generateDocumentOutline"] = true,
            ["printBackground"] = true,
            ["paperWidth"] = 8.27,
            ["paperHeight"] = 11.7,
            ["marginTop"] = 0,
            ["marginRight"] = 0,
            ["marginBottom"] = 0,
            ["marginLeft"] = 0,
        }).WaitAsync(ct).ConfigureAwait(false), InvalidStream);
        if (!reply.TryGetProperty("stream", out var handle) || handle.ValueKind != JsonValueKind.String ||
            handle.GetString() is not { Length: > 0 } stream ||
            (reply.TryGetProperty("data", out var data) &&
                (data.ValueKind != JsonValueKind.String || data.GetString() is not { Length: 0 })))
        {
            throw new InvalidDataException(InvalidStream);
        }

        var document = await Read(session, stream, limit, ct).ConfigureAwait(false);
        await session.SendAsync("IO.close", new Dictionary<string, object> { ["handle"] = stream }).WaitAsync(ct).ConfigureAwait(false);
        return document;
    }

    private static async Task<ReadOnlyMemory<byte>> Read(ICDPSession session, string stream, int limit, CancellationToken ct)
    {
        using var output = new MemoryStream(Math.Min(ChunkLimit, limit));
        var chunk = ArrayPool<byte>.Shared.Rent(ChunkLimit);
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var available = limit - (int)output.Length;
                var wanted = (int)Math.Min(ChunkLimit, available + 1L);
                var reply = RequireResult(await session.SendAsync("IO.read", new Dictionary<string, object>
                {
                    ["handle"] = stream,
                    ["size"] = wanted,
                }).WaitAsync(ct).ConfigureAwait(false), InvalidChunk);
                var count = Decode(reply, chunk.AsSpan(0, wanted), out var finished);
                if (count > available)
                {
                    throw new InvalidDataException("The generated PDF exceeds its byte limit.");
                }

                var needed = (int)output.Length + count;
                if (needed > output.Capacity)
                {
                    output.Capacity = (int)Math.Min(limit, Math.Max(needed, output.Capacity * 2L));
                }

                output.Write(chunk.AsSpan(0, count));
                if (finished)
                {
                    return output.GetBuffer().AsMemory(0, needed);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk, clearArray: true);
        }
    }

    private static int Decode(JsonElement reply, Span<byte> destination, out bool finished)
    {
        if (!reply.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.String ||
            data.GetString() is not { } encoded || encoded.Length > (destination.Length + 2) / 3 * 4 ||
            !reply.TryGetProperty("eof", out var eof) || eof.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            (encoded.Length != 0 && (!reply.TryGetProperty("base64Encoded", out var base64) || base64.ValueKind != JsonValueKind.True)) ||
            !Convert.TryFromBase64String(encoded, destination, out var read))
        {
            throw new InvalidDataException(InvalidChunk);
        }

        finished = eof.ValueKind == JsonValueKind.True;
        if (read == 0 && !finished)
        {
            throw new InvalidDataException(InvalidChunk);
        }

        return read;
    }

    private static JsonElement RequireResult(JsonElement? result, string failure) =>
        result is { ValueKind: JsonValueKind.Object } value ? value : throw new InvalidDataException(failure);
}
