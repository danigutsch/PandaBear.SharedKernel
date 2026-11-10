namespace PandaBear.DocumentRendering;

/// <summary>
/// Defines one block of a document section that keeps its position among the other blocks: a <see cref="DocumentParagraph"/> or a
/// <see cref="DocumentList"/>. Use blocks, through <see cref="DocumentSection.Blocks"/>, when prose must follow a list.
/// The set of blocks is closed: only this package defines them.
/// </summary>
public abstract record DocumentBlock
{
    /// <summary>Initializes a new instance of the <see cref="DocumentBlock"/> class; only this package derives blocks.</summary>
    private protected DocumentBlock()
    {
    }
}
