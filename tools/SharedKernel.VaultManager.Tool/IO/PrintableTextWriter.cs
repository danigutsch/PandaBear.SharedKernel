using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PandaBear.VaultManager.Tool.IO;

/// <summary>
/// Writes text to another writer as <see cref="PrintableText.OneLine"/> does, except that line breaks stay: a line feed,
/// and a carriage return directly before a line feed in the same write. Every other control character and every
/// bidirectional embedding, override, and isolate becomes <see cref="PrintableText.Replacement"/>, and a tab, any other
/// carriage return, and a line or paragraph separator become spaces. Each write goes through at once; the writer does not own the other writer.
/// </summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The other writer belongs to the caller, such as the console.")]
internal sealed class PrintableTextWriter : TextWriter
{
    private readonly TextWriter inner;

    /// <summary>Creates a writer that writes printable text to another writer, with the line break of that writer.</summary>
    /// <param name="inner">Writer that receives the printable text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
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
    public override void Write(char value) => inner.Write(value == '\n' ? "\n" : PrintableText.OneLine(value.ToString()));

    /// <inheritdoc/>
    public override void Write(string? value)
    {
        if (value is not null)
        {
            inner.Write(Printable(value));
        }
    }

    /// <inheritdoc/>
    public override void Write(char[] buffer, int index, int count) => inner.Write(Printable(new string(buffer, index, count)));

    /// <inheritdoc/>
    public override void Flush() => inner.Flush();

    private static string Printable(string text)
    {
        string printable = PrintableText.OneLine(text);
        if (ReferenceEquals(printable, text))
        {
            return text;
        }

        return string.Create(text.Length, (Source: text, Printable: printable), static (span, texts) =>
        {
            string source = texts.Source;
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                bool lineBreak = character == '\n' || (character == '\r' && index + 1 < source.Length && source[index + 1] == '\n');
                span[index] = lineBreak ? character : texts.Printable[index];
            }
        });
    }
}
