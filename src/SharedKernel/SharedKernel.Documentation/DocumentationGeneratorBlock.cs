namespace PandaBear.Documentation;

internal sealed class DocumentationGeneratorBlock
{
    /// <summary>Marker name of the generated block inside the target document.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Block kind that selects the renderer, such as mermaid-flowchart or endpoint-routes.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Repository-relative path of the Markdown document that receives the block.</summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>Mermaid flowchart header line, such as the left-to-right direction, for mermaid-flowchart blocks.</summary>
    public string Flowchart { get; set; } = MermaidDiagram.LeftRight;

    /// <summary>Literal Mermaid body lines for mermaid-flowchart blocks.</summary>
    public List<string> Lines { get; set; } = [];

    /// <summary>Repository-relative file or directory the block reads its facts from.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Name of the project filter that selects which projects a project-references block includes.</summary>
    public string ProjectFilter { get; set; } = string.Empty;

    /// <summary>Display label to use per resource name in apphost-resources blocks.</summary>
    public Dictionary<string, string> Labels { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Route prefix to prepend per map-group variable name when building endpoint paths.</summary>
    public Dictionary<string, string> RoutePrefixes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Method names whose argument supplies an endpoint name, such as WithName.</summary>
    public List<string> EndpointNameMethods { get; set; } = ["WithName"];

    /// <summary>Audience to assign to endpoints per source file path.</summary>
    public Dictionary<string, string> SourceAudiences { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Audience to assign to endpoints per route path.</summary>
    public Dictionary<string, string> RouteAudiences { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Audience for endpoints that match neither a source nor a route audience.</summary>
    public string DefaultAudience { get; set; } = "unspecified";

    /// <summary>Label of the trigger node in github-actions-jobs blocks.</summary>
    public string TriggerLabel { get; set; } = string.Empty;

    /// <summary>Label of the root node in github-actions-workflows blocks.</summary>
    public string RootLabel { get; set; } = string.Empty;

    /// <summary>Workflow file name left out of the github-actions-workflows inventory.</summary>
    public string ExcludedWorkflowFileName { get; set; } = string.Empty;
}
