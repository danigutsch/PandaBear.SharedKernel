using System.Text.RegularExpressions;

namespace SharedKernel.Documentation;

/// <summary>
/// Builds Mermaid diagram Markdown blocks.
/// </summary>
internal static partial class MermaidDiagram
{
    /// <summary>
    /// Top-to-bottom Mermaid flowchart direction.
    /// </summary>
    public const string TopBottom = "flowchart TB";

    /// <summary>
    /// Left-to-right Mermaid flowchart direction.
    /// </summary>
    public const string LeftRight = "flowchart LR";

    /// <summary>
    /// Builds a fenced Mermaid block.
    /// </summary>
    public static string Build(string flowchart, IEnumerable<string> body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flowchart);
        ArgumentNullException.ThrowIfNull(body);

        return string.Join('\n', ["```mermaid", flowchart, .. body, "```"]);
    }

    /// <summary>
    /// Converts a label into a Mermaid-safe node identifier.
    /// </summary>
    public static string NodeId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var cleaned = NonWordRegex().Replace(value, "_");
        return cleaned.Length > 0 && char.IsAsciiDigit(cleaned[0]) ? $"n_{cleaned}" : cleaned;
    }

    /// <summary>
    /// Formats dependency edges as Mermaid node declarations and arrows.
    /// </summary>
    public static IEnumerable<string> FormatEdges(IReadOnlyCollection<(string Source, string Target)> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        return edges.Count == 0 ? ["    empty[No project references]"] : edges.Select(edge => $"    {NodeId(edge.Source)}[{edge.Source}] --> {NodeId(edge.Target)}[{edge.Target}]");
    }

    [GeneratedRegex(@"\W", RegexOptions.CultureInvariant)]
    private static partial Regex NonWordRegex();
}
