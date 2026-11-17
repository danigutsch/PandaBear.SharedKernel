using System.Diagnostics.CodeAnalysis;
using System.Text;
using PandaBear.Vault.IO;

namespace PandaBear.VaultManager.Tool;

/// <summary>
/// Writes text to another writer as <see cref="PrintableText.OneLine"/> does, except that line breaks stay: a line feed,
/// and a carriage return directly before a line feed in the same write. Every other control character, every format
/// character, such as a bidirectional mark, embedding, override, or isolate or a zero-width character, every other
/// default ignorable code point except a variation selector, and every unpaired surrogate becomes
/// <see cref="PrintableText.Replacement"/>, and a tab, any other carriage return, and a line or paragraph separator become
/// spaces. A zero width joiner or non-joiner and the tag characters stay where <see cref="PrintableText.OneLine"/> keeps
/// them, judged within one line of one write, so a sequence split between two writes or two lines is replaced.
/// Each write goes through at once, except a high surrogate at its end, which waits for the low surrogate that the next
/// write may start with, so that a surrogate pair written in two parts stays. <see cref="Flush"/> keeps that high
/// surrogate waiting; disposal writes a high surrogate still waiting as <see cref="PrintableText.Replacement"/> and
/// flushes the other writer. The writer does not own the other writer and does not dispose it.
/// </summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The other writer belongs to the caller, such as the console.")]
internal sealed class PrintableTextWriter : TextWriter
{
    private readonly TextWriter inner;
    private char? pendingHighSurrogate;

    /// <summary>Creates a writer that writes printable text to another writer, with the line break of that writer.</summary>
    /// <param name="inner">Writer that receives the printable text.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is <see langword="null"/>.</exception>
    public PrintableTextWriter(TextWriter inner)
        : base(inner?.FormatProvider)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.inner = inner;
        NewLine = inner.NewLine;
    }

    /// <inheritdoc/>
    public override Encoding Encoding => inner.Encoding;

    /// <inheritdoc/>
    public override void Write(char value) => WritePrintable(value.ToString());

    /// <inheritdoc/>
    public override void Write(string? value)
    {
        if (value is not null)
        {
            WritePrintable(value);
        }
    }

    /// <inheritdoc/>
    public override void Write(char[] buffer, int index, int count) => WritePrintable(new string(buffer, index, count));

    /// <inheritdoc/>
    public override void Flush() => inner.Flush();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            WritePendingHighSurrogate();
            inner.Flush();
        }

        base.Dispose(disposing);
    }

    private static string Printable(string text)
    {
        if (text.AsSpan().IndexOfAny('\r', '\n') < 0)
        {
            return PrintableText.OneLine(text);
        }

        // OneLine can shorten the text, so each line is made printable on its own and the line breaks are kept between.
        var lines = new StringBuilder(text.Length);
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            int lineBreak = text[index] == '\n' ? 1
                : text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? 2
                : 0;
            if (lineBreak > 0)
            {
                lines.Append(PrintableText.OneLine(text[start..index])).Append(text, index, lineBreak);
                index += lineBreak - 1;
                start = index + 1;
            }
        }

        return lines.Append(PrintableText.OneLine(text[start..])).ToString();
    }

    private void WritePrintable(string text)
    {
        if (pendingHighSurrogate is char highSurrogate)
        {
            pendingHighSurrogate = null;
            text = highSurrogate + text;
        }

        if (text.Length > 0 && char.IsHighSurrogate(text[^1]))
        {
            pendingHighSurrogate = text[^1];
            text = text[..^1];
        }

        if (text.Length > 0)
        {
            inner.Write(Printable(text));
        }
    }

    private void WritePendingHighSurrogate()
    {
        if (pendingHighSurrogate is not null)
        {
            pendingHighSurrogate = null;
            inner.Write(PrintableText.Replacement);
        }
    }
}
