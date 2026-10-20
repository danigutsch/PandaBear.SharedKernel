namespace SharedKernel.Documentation;

internal sealed class DocumentationFactCheck
{
    public string Name { get; set; } = string.Empty;

    public string DocumentPath { get; set; } = string.Empty;

    public string MarkerName { get; set; } = string.Empty;

    public string FactName { get; set; } = string.Empty;

    public List<string> ContentBlockMarkers { get; set; } = [];

    public List<string> ExpectedIdentifiers { get; set; } = [];

    public List<DocumentationSourceMethod> SwitchSources { get; set; } = [];

    public List<DocumentationSourceMethod> RegistrationSources { get; set; } = [];

    public List<string> IncludedIdentifierFragments { get; set; } = [];

    public List<string> IncludedIdentifiers { get; set; } = [];

    public List<DocumentationInvocationRequirement> InvocationRequirements { get; set; } = [];
}
