namespace PandaBear.Documentation;

/// <summary>One configured comparison between a fact list marked in a Markdown document and the C# source that is its source of truth.</summary>
internal sealed class DocumentationFactCheck
{
    /// <summary>Check name used in failure messages.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Repository-relative path of the Markdown document that holds the documented fact.</summary>
    public string DocumentPath { get; set; } = string.Empty;

    /// <summary>Name of the generated-content marker that wraps the documented fact in the document.</summary>
    public string MarkerName { get; set; } = string.Empty;

    /// <summary>Name of the fact entries read from inside the marker.</summary>
    public string FactName { get; set; } = string.Empty;

    /// <summary>Names of additional marked content blocks in the document that must be well formed.</summary>
    public List<string> ContentBlockMarkers { get; set; } = [];

    /// <summary>Identifiers the document must list, given literally instead of read from source.</summary>
    public List<string> ExpectedIdentifiers { get; set; } = [];

    /// <summary>Methods whose switch case labels supply the identifiers the document must list.</summary>
    public List<DocumentationSourceMethod> SwitchSources { get; set; } = [];

    /// <summary>Sources whose registration calls supply the identifiers the document must list.</summary>
    public List<DocumentationSourceMethod> RegistrationSources { get; set; } = [];

    /// <summary>Substrings that select which registration identifiers count; an identifier containing any fragment is included.</summary>
    public List<string> IncludedIdentifierFragments { get; set; } = [];

    /// <summary>Exact registration identifiers that count toward the documented list.</summary>
    public List<string> IncludedIdentifiers { get; set; } = [];

    /// <summary>Source invocations that must occur the expected number of times with the expected arguments.</summary>
    public List<DocumentationInvocationRequirement> InvocationRequirements { get; set; } = [];
}
