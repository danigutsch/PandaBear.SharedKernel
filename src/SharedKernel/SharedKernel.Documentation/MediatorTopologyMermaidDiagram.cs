using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharedKernel.Documentation;

/// <summary>Renders the versioned call graph emitted by the mediator source generator.</summary>
internal static class MediatorTopologyMermaidDiagram
{
    private const int SupportedSchemaVersion = 1;

    /// <summary>Builds a Mermaid flowchart from a generated C# call graph file.</summary>
    /// <exception cref="InvalidOperationException">The call graph is missing or has an invalid or unsupported schema.</exception>
    public static string Build(string path)
    {
        var values = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot()
            .Members.OfType<FileScopedNamespaceDeclarationSyntax>()
            .Where(static member => member.Name.ToString() == "SharedKernel.Mediator.Generated")
            .SelectMany(static member => member.Members.OfType<ClassDeclarationSyntax>())
            .Where(static member => member.Identifier.ValueText == "MediatorCallGraph")
            .SelectMany(static member => member.Members.OfType<PropertyDeclarationSyntax>())
            .Where(static member => member.Identifier.ValueText == "Json")
            .Select(static member => member.Initializer?.Value)
            .Take(2)
            .ToArray();
        if (values is not [LiteralExpressionSyntax literal] || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            throw new InvalidOperationException($"Missing the generated mediator call graph JSON in '{path}'.");
        }

        using var document = ParseGraph(literal.Token.ValueText, path);
        var graph = document.RootElement;
        if (graph.ValueKind != JsonValueKind.Object || !graph.TryGetProperty("schemaVersion", out var version)
            || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema)
            || schema != SupportedSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported mediator call graph schema in '{path}'. Regenerate it with schema version {SupportedSchemaVersion}.");
        }

        try
        {
            return Render(graph);
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new InvalidOperationException($"Invalid mediator call graph JSON in '{path}'.", exception);
        }
    }

    private static JsonDocument ParseGraph(string json, string path)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Invalid mediator call graph JSON in '{path}'.", exception);
        }
    }

    private static string Render(JsonElement graph)
    {
        var lines = new List<string>();
        foreach (var request in graph.GetProperty("requests").EnumerateArray())
        {
            AddRequest(lines, request, Text(request, "kind"));
        }

        foreach (var notification in graph.GetProperty("notifications").EnumerateArray())
        {
            var dispatch = Text(notification, "dispatch");
            var source = AddNode(lines, $"notification {Text(notification, "notification")} ({dispatch})");
            foreach (var handler in notification.GetProperty("handlers").EnumerateArray())
            {
                var target = AddNode(lines, handler.GetString() ?? throw new FormatException("A notification handler name is null."));
                lines.Add($"    {source} --> {target}");
            }
        }

        foreach (var request in graph.GetProperty("streamRequests").EnumerateArray())
        {
            AddRequest(lines, request, "stream");
        }

        if (lines.Count == 0)
        {
            AddNode(lines, "No mediator contracts");
        }

        return MermaidDiagram.Build(MermaidDiagram.LeftRight, lines);
    }

    private static void AddRequest(List<string> lines, JsonElement request, string kind)
    {
        var previous = AddNode(lines, $"{kind} {Text(request, "request")}");
        foreach (var pipeline in request.GetProperty("pipelines").EnumerateArray())
        {
            var next = AddNode(lines, pipeline.GetString() ?? throw new FormatException("A pipeline name is null."));
            lines.Add($"    {previous} --> {next}");
            previous = next;
        }

        var handler = Text(request, "handler");
        var handlerNode = AddNode(lines, handler == "<none>" ? "No unique handler" : handler);
        lines.Add($"    {previous} --> {handlerNode}");
    }

    private static string AddNode(List<string> lines, string label)
    {
        var id = "mediator_" + lines.Count.ToString(CultureInfo.InvariantCulture);
        var escaped = label.Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace("#", "#35;", StringComparison.Ordinal)
            .Replace("\"", "#34;", StringComparison.Ordinal)
            .Replace("<", "#60;", StringComparison.Ordinal)
            .Replace(">", "#62;", StringComparison.Ordinal)
            .Replace("&", "#38;", StringComparison.Ordinal)
            .Replace("`", "#96;", StringComparison.Ordinal)
            .Replace("\r", "#13;", StringComparison.Ordinal)
            .Replace("\n", "#10;", StringComparison.Ordinal);

        lines.Add($"    {id}[\"{escaped}\"]");
        return id;
    }

    private static string Text(JsonElement owner, string name) =>
        owner.GetProperty(name).GetString() ?? throw new FormatException($"The '{name}' property is null.");
}
