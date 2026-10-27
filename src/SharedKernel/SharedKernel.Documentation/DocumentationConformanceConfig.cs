namespace PandaBear.Documentation;

/// <summary>Configuration file model for the documentation conformance checker.</summary>
internal sealed class DocumentationConformanceConfig
{
    /// <summary>Fact checks that each compare a marked documentation fact with its C# source of truth.</summary>
    public List<DocumentationFactCheck> Checks { get; set; } = [];
}
