namespace PandaBear.Documentation;

internal sealed class DocumentationGeneratorBlock
{
    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public string Flowchart { get; set; } = MermaidDiagram.LeftRight;

    public List<string> Lines { get; set; } = [];

    public string SourcePath { get; set; } = string.Empty;

    public string ProjectFilter { get; set; } = string.Empty;

    public Dictionary<string, string> Labels { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> RoutePrefixes { get; set; } = new(StringComparer.Ordinal);

    public List<string> EndpointNameMethods { get; set; } = ["WithName"];

    public Dictionary<string, string> SourceAudiences { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> RouteAudiences { get; set; } = new(StringComparer.Ordinal);

    public string DefaultAudience { get; set; } = "unspecified";

    public string TriggerLabel { get; set; } = string.Empty;

    public string RootLabel { get; set; } = string.Empty;

    public string ExcludedWorkflowFileName { get; set; } = string.Empty;
}
