using System.Text;
using System.Text.RegularExpressions;

namespace SharedKernel.Documentation;

/// <summary>
/// Builds Mermaid diagrams from simple .NET Aspire AppHost resource declarations.
/// </summary>
internal static partial class AppHostMermaidDiagram
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// Builds an AppHost resource relationship diagram.
    /// </summary>
    public static string Build(string appHostPath, IReadOnlyDictionary<string, string> labels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostPath);
        ArgumentNullException.ThrowIfNull(labels);

        var lines = File.ReadAllLines(appHostPath, Utf8NoBom);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        var edges = new List<(string Source, string Target)>();

        AddAppHostAssignments(lines, labels, variables, edges);
        AddAppHostInvocations(lines, labels, variables, edges);

        var diagramLines = new List<string>();
        foreach (var (key, label) in variables.OrderBy(item => item.Value, StringComparer.Ordinal))
        {
            diagramLines.Add($"    {MermaidDiagram.NodeId(key)}[{label}]");
        }

        foreach (var (source, target) in edges.Distinct().OrderBy(edge => edge.Source, StringComparer.Ordinal).ThenBy(edge => edge.Target, StringComparer.Ordinal))
        {
            diagramLines.Add($"    {MermaidDiagram.NodeId(source)} --> {MermaidDiagram.NodeId(target)}");
        }

        return MermaidDiagram.Build(MermaidDiagram.LeftRight, diagramLines);
    }

    private static void AddAppHostAssignments(string[] lines, IReadOnlyDictionary<string, string> labels, Dictionary<string, string> variables, List<(string Source, string Target)> edges)
    {
        foreach (var line in lines)
        {
            var match = AppHostAssignmentRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            AddAppHostAssignment(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value, labels, variables, edges);
        }
    }

    private static void AddAppHostAssignment(
        string variable,
        string receiver,
        string method,
        string args,
        IReadOnlyDictionary<string, string> labels,
        Dictionary<string, string> variables,
        List<(string Source, string Target)> edges)
    {
        if (!method.StartsWith("Add", StringComparison.Ordinal))
        {
            return;
        }

        variables[variable] = AppHostLabel(variable, method, labels);
        var receiverParts = receiver.Split('.');
        var receiverName = receiverParts[^1];
        AddEdgeIfKnown(edges, variable, receiverName, variables);
        AddArgumentEdges(edges, variable, args, variables);
    }

    private static void AddAppHostInvocations(string[] lines, IReadOnlyDictionary<string, string> labels, Dictionary<string, string> variables, List<(string Source, string Target)> edges)
    {
        foreach (var line in lines)
        {
            var match = AppHostInvocationRegex().Match(line);
            if (!match.Success || line.Contains("var ", StringComparison.Ordinal))
            {
                continue;
            }

            var method = match.Groups[1].Value;
            var args = match.Groups[2].Value;
            var node = method[3..];
            variables[node] = AppHostLabel(node, method, labels);
            AddArgumentEdges(edges, node, args, variables);
        }
    }

    private static void AddArgumentEdges(List<(string Source, string Target)> edges, string source, string args, Dictionary<string, string> variables)
    {
        foreach (var arg in ParseAppHostArgs(args))
        {
            AddEdgeIfKnown(edges, source, arg, variables);
        }
    }

    private static void AddEdgeIfKnown(List<(string Source, string Target)> edges, string source, string target, Dictionary<string, string> variables)
    {
        if (variables.ContainsKey(target))
        {
            edges.Add((source, target));
        }
    }

    private static IEnumerable<string> ParseAppHostArgs(string args) =>
        args.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(arg => AppHostArgumentRegex().IsMatch(arg));

    private static string AppHostLabel(string variable, string method, IReadOnlyDictionary<string, string> labels)
    {
        if (labels.TryGetValue(variable, out var label))
        {
            return label;
        }

        var value = method.StartsWith("Add", StringComparison.Ordinal) ? method[3..] : variable;
        return PascalCaseWordRegex().Replace(value, " $1").Replace(" Web", ".Web", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"var\s+(\w+)\s+=\s+(?:.+?\?\s+)?([\w.]+)\.(\w+)\(([^)]*)\)(?:\s*:\s*null)?;", RegexOptions.CultureInvariant)]
    private static partial Regex AppHostAssignmentRegex();

    [GeneratedRegex(@"builder\.(Add\w+)\(([^)]*)\);", RegexOptions.CultureInvariant)]
    private static partial Regex AppHostInvocationRegex();

    [GeneratedRegex(@"^\w+$", RegexOptions.CultureInvariant)]
    private static partial Regex AppHostArgumentRegex();

    [GeneratedRegex(@"(?<!^)([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex PascalCaseWordRegex();
}
