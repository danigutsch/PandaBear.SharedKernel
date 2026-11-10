namespace PandaBear.DocumentRendering;

/// <summary>
/// Defines an ordered semantic section of a document.
/// </summary>
public sealed record DocumentSection(string Heading, IReadOnlyList<DocumentField> Fields)
{
    /// <summary>
    /// Gets the optional prose of the section, rendered in order before the fields. It is empty by default.
    /// </summary>
    public IReadOnlyList<DocumentParagraph> Paragraphs
    {
        get;
        init;
    } = [];

    /// <summary>
    /// Gets the optional lists of the section, rendered in order after the paragraphs and before the fields. It is empty by default.
    /// </summary>
    public IReadOnlyList<DocumentList> Lists
    {
        get;
        init;
    } = [];

    /// <summary>
    /// Gets the optional blocks of the section, rendered in the given order after the <see cref="Paragraphs"/> and the
    /// <see cref="Lists"/> and before the fields. Use them when a paragraph must follow a list, which <see cref="Paragraphs"/>
    /// and <see cref="Lists"/> cannot express because they render separately. It is empty by default.
    /// </summary>
    public IReadOnlyList<DocumentBlock> Blocks
    {
        get;
        init;
    } = [];
}
