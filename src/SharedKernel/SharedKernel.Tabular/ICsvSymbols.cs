namespace PandaBear.Tabular;

/// <summary>The structural characters of CSV in one text encoding, and how a field's code units become a string.</summary>
/// <typeparam name="T">The code unit: <see cref="char"/> for UTF-16 or <see cref="byte"/> for UTF-8.</typeparam>
internal interface ICsvSymbols<T>
    where T : unmanaged, IEquatable<T>
{
    /// <summary>Gets the field separator.</summary>
    static abstract T Comma
    {
        get;
    }

    /// <summary>Gets the quote.</summary>
    static abstract T Quote
    {
        get;
    }

    /// <summary>Gets the carriage return.</summary>
    static abstract T CarriageReturn
    {
        get;
    }

    /// <summary>Gets the line feed.</summary>
    static abstract T LineFeed
    {
        get;
    }

    /// <summary>Gets the space.</summary>
    static abstract T Space
    {
        get;
    }

    /// <summary>Gets the horizontal tab.</summary>
    static abstract T Tab
    {
        get;
    }

    /// <summary>Gets the byte order mark in this encoding.</summary>
    static abstract ReadOnlySpan<T> ByteOrderMark
    {
        get;
    }

    /// <summary>Converts a field's code units to a string.</summary>
    /// <param name="text">The code units.</param>
    /// <returns>The string.</returns>
    static abstract string Decode(ReadOnlySpan<T> text);
}
