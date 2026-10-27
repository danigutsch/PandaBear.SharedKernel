namespace PandaBear.Documentation;

/// <summary>Configuration file model for the documentation generator.</summary>
internal sealed class DocumentationGeneratorConfig
{
    /// <summary>Repository-relative directory that contains the Markdown documents the generator may update.</summary>
    public string DocsPath { get; set; } = string.Empty;

    /// <summary>Name of the generator that is written into the generated-block markers.</summary>
    public string GeneratorName { get; set; } = string.Empty;

    /// <summary>Generated blocks to render and write into their target documents.</summary>
    public List<DocumentationGeneratorBlock> Blocks { get; set; } = [];
}
