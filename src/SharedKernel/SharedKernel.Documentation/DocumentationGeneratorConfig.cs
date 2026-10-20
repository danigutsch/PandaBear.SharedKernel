namespace SharedKernel.Documentation;

internal sealed class DocumentationGeneratorConfig
{
    public string DocsPath { get; set; } = string.Empty;

    public string GeneratorName { get; set; } = string.Empty;

    public List<DocumentationGeneratorBlock> Blocks { get; set; } = [];
}
