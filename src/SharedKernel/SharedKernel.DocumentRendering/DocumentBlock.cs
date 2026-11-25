namespace SharedKernel.DocumentRendering;

/// <summary>
/// Defines a <see cref="DocumentParagraph"/>, <see cref="DocumentList"/>, or <see cref="DocumentLink"/> that keeps its
/// position among the other elements of <see cref="DocumentSection.Blocks"/>.
/// The set of blocks is closed: only this package defines them.
/// </summary>
public abstract record DocumentBlock
{
    /// <summary>Initializes a new instance of the <see cref="DocumentBlock"/> class; only this package derives blocks.</summary>
    private protected DocumentBlock()
    {
    }
}
