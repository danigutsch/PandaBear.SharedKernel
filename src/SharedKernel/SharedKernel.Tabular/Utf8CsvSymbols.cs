using System.Text;

namespace SharedKernel.Tabular;

/// <summary>
/// The CSV symbols of UTF-8 bytes. Every structural character is ASCII, and no byte of a multibyte UTF-8 sequence is ASCII, so fields are
/// found on the bytes and only each field is decoded; invalid UTF-8 is replaced per field as the decoder does.
/// </summary>
internal readonly struct Utf8CsvSymbols : ICsvSymbols<byte>
{
    /// <inheritdoc />
    public static byte Comma => (byte)',';

    /// <inheritdoc />
    public static byte Quote => (byte)'"';

    /// <inheritdoc />
    public static byte CarriageReturn => (byte)'\r';

    /// <inheritdoc />
    public static byte LineFeed => (byte)'\n';

    /// <inheritdoc />
    public static byte Space => (byte)' ';

    /// <inheritdoc />
    public static byte Tab => (byte)'\t';

    /// <inheritdoc />
    public static ReadOnlySpan<byte> ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <inheritdoc />
    public static string Decode(ReadOnlySpan<byte> text) => Encoding.UTF8.GetString(text);
}
