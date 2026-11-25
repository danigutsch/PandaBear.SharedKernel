namespace SharedKernel.PageSections;

/// <summary>A quote of a section and, optionally, who said it.</summary>
public sealed record PageQuote
{
    /// <summary>Initializes a quote.</summary>
    /// <param name="text">The words.</param>
    /// <param name="cite">Who said them.</param>
    internal PageQuote(string text, string? cite)
    {
        Text = text;
        Cite = cite;
    }

    /// <summary>Gets the quoted words.</summary>
    public string Text { get; }

    /// <summary>Gets the optional name of who said them.</summary>
    public string? Cite { get; }
}
