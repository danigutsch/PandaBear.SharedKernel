namespace SharedKernel.Tabular;

/// <summary>The CSV symbols of UTF-16 text.</summary>
internal readonly struct Utf16CsvSymbols : ICsvSymbols<char>
{
    /// <inheritdoc />
    public static char Comma => ',';

    /// <inheritdoc />
    public static char Quote => '"';

    /// <inheritdoc />
    public static char CarriageReturn => '\r';

    /// <inheritdoc />
    public static char LineFeed => '\n';

    /// <inheritdoc />
    public static char Space => ' ';

    /// <inheritdoc />
    public static char Tab => '\t';

    /// <inheritdoc />
    public static ReadOnlySpan<char> ByteOrderMark => "﻿";

    /// <inheritdoc />
    public static string Decode(ReadOnlySpan<char> text) => new(text);
}
