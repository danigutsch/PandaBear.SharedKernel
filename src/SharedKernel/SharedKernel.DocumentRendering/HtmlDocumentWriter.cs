using System.Net;
using System.Text;

namespace PandaBear.DocumentRendering;

/// <summary>Accumulates semantic HTML and enforces the optional UTF-8 output budget.</summary>
/// <param name="remainingBytes">The initial byte budget, or null for unbounded rendering.</param>
/// <param name="ct">Cancellation token checked during rendering.</param>
internal sealed class HtmlDocumentWriter(int? remainingBytes, CancellationToken ct)
{
    private const int EncodingChunkLength = 512;
    private readonly StringBuilder _builder = new();
    private int? _remainingBytes = remainingBytes;

    /// <summary>Appends trusted markup within the remaining UTF-8 budget.</summary>
    /// <exception cref="InvalidDataException">The output would exceed the UTF-8 budget.</exception>
    internal HtmlDocumentWriter Append(ReadOnlySpan<char> value)
    {
        ct.ThrowIfCancellationRequested();
        if (_remainingBytes is { } remaining)
        {
            // UTF-8 never uses fewer bytes than the number of UTF-16 code units.
            var length = value.Length > remaining ? value.Length : Encoding.UTF8.GetByteCount(value);
            if (length > remaining)
            {
                throw new InvalidDataException("The document HTML exceeds its byte limit.");
            }

            _remainingBytes = remaining - length;
        }

        _builder.Append(value);
        return this;
    }

    /// <summary>Appends a trusted markup character.</summary>
    internal HtmlDocumentWriter Append(char value) => Append(stackalloc char[] { value });

    /// <summary>Escapes text and appends it without exceeding the remaining UTF-8 budget.</summary>
    internal HtmlDocumentWriter AppendEncoded(string? value)
    {
        if (_remainingBytes is null)
        {
            return Append(WebUtility.HtmlEncode(value ?? string.Empty));
        }

        var text = value.AsSpan();
        while (!text.IsEmpty)
        {
            ct.ThrowIfCancellationRequested();
            var length = Math.Min(EncodingChunkLength, text.Length);
            // Preserve WebUtility's handling of supplementary Unicode characters at chunk boundaries.
            if (length < text.Length && char.IsSurrogatePair(text[length - 1], text[length]))
            {
                length--;
            }

            Append(WebUtility.HtmlEncode(text[..length].ToString()));
            text = text[length..];
        }

        return this;
    }

    /// <summary>Returns the complete HTML encoded as UTF-8.</summary>
    internal byte[] ToUtf8()
    {
        ct.ThrowIfCancellationRequested();
        var bytes = Encoding.UTF8.GetBytes(_builder.ToString());
        ct.ThrowIfCancellationRequested();
        return bytes;
    }
}
