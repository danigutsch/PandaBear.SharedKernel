// Adapted from ViajantesTurismo. See NOTICE.ViajantesTurismo.txt for the MIT license and attribution.

using System.Net;

namespace SharedKernel.Documentation;

/// <summary>Renders typed data ownership facts in the shared Mermaid format.</summary>
internal static class DataOwnershipMermaidDiagram
{
    /// <summary>Renders the service and data store trust boundaries and access relationships.</summary>
    internal static string Build(DataOwnershipDiagram diagram)
    {
        Validate(diagram);
        var lines = new List<string>();
        foreach (var boundary in diagram.Boundaries.OrderBy(boundary => boundary.Id, StringComparer.Ordinal))
        {
            var services = NodesWithin(diagram.Services, boundary.Id);
            var stores = NodesWithin(diagram.Stores, boundary.Id);
            if (services.Length == 0 && stores.Length == 0)
            {
                continue;
            }

            lines.Add($"    subgraph {BoundaryNodeId(boundary.Id)}[\"{WebUtility.HtmlEncode(boundary.Label)}\"]");
            lines.AddRange(services.Concat(stores).Select(Node));
            lines.Add("    end");
        }

        lines.AddRange(diagram.Accesses.OrderBy(access => access.StoreId, StringComparer.Ordinal)
            .ThenBy(access => access.ServiceId, StringComparer.Ordinal)
            .ThenBy(access => access.Label, StringComparer.Ordinal)
            .Select(access => $"    {access.ServiceId} -->|\"{WebUtility.HtmlEncode(access.Label)}\"| {access.StoreId}"));
        return MermaidDiagram.Build(MermaidDiagram.LeftRight, lines);
    }

    private static DataOwnershipNode[] NodesWithin(IReadOnlyList<DataOwnershipNode> nodes, string boundaryId) =>
        [.. nodes.Where(node => string.Equals(node.BoundaryId, boundaryId, StringComparison.Ordinal))
            .OrderBy(node => node.Id, StringComparer.Ordinal)];

    private static string BoundaryNodeId(string boundaryId) => $"boundary_{boundaryId}";

    private static string Node(DataOwnershipNode node) =>
        $"        {node.Id}[\"{string.Join("<br/>", node.LabelLines.Select(WebUtility.HtmlEncode))}\"]";

    private static void Validate(DataOwnershipDiagram diagram)
    {
        ArgumentNullException.ThrowIfNull(diagram.Boundaries);
        ArgumentNullException.ThrowIfNull(diagram.Services);
        ArgumentNullException.ThrowIfNull(diagram.Stores);
        ArgumentNullException.ThrowIfNull(diagram.Accesses);

        var boundaryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var boundary in diagram.Boundaries)
        {
            ArgumentNullException.ThrowIfNull(boundary);
            if (!IsIdentifier(boundary.Id) || !IsLabel(boundary.Label) || !boundaryIds.Add(boundary.Id))
            {
                throw new ArgumentException($"Invalid or duplicate data ownership boundary '{boundary.Id}'.", nameof(diagram));
            }
        }

        var nodeIds = boundaryIds.Select(BoundaryNodeId).ToHashSet(StringComparer.Ordinal);
        foreach (var node in diagram.Services.Concat(diagram.Stores))
        {
            ArgumentNullException.ThrowIfNull(node);
            if (!IsIdentifier(node.Id) || !nodeIds.Add(node.Id) || !boundaryIds.Contains(node.BoundaryId)
                || node.LabelLines is not { Count: > 0 } || node.LabelLines.Any(label => !IsLabel(label)))
            {
                throw new ArgumentException($"Invalid, duplicate, or unassigned data ownership node '{node.Id}'.", nameof(diagram));
            }
        }

        var serviceIds = diagram.Services.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var storeIds = diagram.Stores.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var access in diagram.Accesses)
        {
            ArgumentNullException.ThrowIfNull(access);
            if (!serviceIds.Contains(access.ServiceId) || !storeIds.Contains(access.StoreId) || !IsLabel(access.Label))
            {
                throw new ArgumentException($"Invalid data ownership access from '{access.ServiceId}' to '{access.StoreId}'.", nameof(diagram));
            }
        }
    }

    private static bool IsIdentifier(string? value) =>
        value is { Length: > 0 } && char.IsAsciiLetter(value[0]) && value != "end"
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static bool IsLabel(string? value) => !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
}
