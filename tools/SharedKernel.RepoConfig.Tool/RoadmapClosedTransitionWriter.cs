using System.Text;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

internal static class RoadmapClosedTransitionWriter
{
    public static string AddToManifest(
        string manifestContent,
        IReadOnlyList<RoadmapClosedItemTransition> approvedTransitions)
    {
        ArgumentNullException.ThrowIfNull(manifestContent);
        ArgumentNullException.ThrowIfNull(approvedTransitions);

        using var document = JsonDocument.Parse(manifestContent);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Roadmap reconciliation manifest root must be a JSON object.");
        }

        Dictionary<int, string> transitions = [];
        HashSet<string> itemIds = new(StringComparer.Ordinal);
        var foundTransitions = false;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!string.Equals(property.Name, "closedItemTransitions", StringComparison.Ordinal))
            {
                continue;
            }

            if (foundTransitions || property.Value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Roadmap reconciliation manifest closed item transitions are invalid.");
            }

            foundTransitions = true;
            foreach (var entry in property.Value.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || !entry.TryGetProperty("issue", out var issueElement)
                    || !issueElement.TryGetInt32(out var issue)
                    || issue < 1
                    || !entry.TryGetProperty("roadmapItem", out var itemElement)
                    || itemElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException("Roadmap reconciliation manifest closed item transitions are invalid.");
                }

                var roadmapItem = itemElement.GetString() ?? string.Empty;
                RoadmapAutomationPlan.ValidateRoadmapItemId(roadmapItem);
                if (!transitions.TryAdd(issue, roadmapItem) || !itemIds.Add(roadmapItem))
                {
                    throw new InvalidOperationException("Roadmap reconciliation manifest closed item transitions are invalid.");
                }
            }
        }

        if (!foundTransitions)
        {
            throw new InvalidOperationException("Roadmap reconciliation manifest does not define closedItemTransitions.");
        }

        foreach (var transition in approvedTransitions)
        {
            ArgumentNullException.ThrowIfNull(transition);
            RoadmapAutomationPlan.ValidateRoadmapItemId(transition.RoadmapItem);
            if (transitions.TryGetValue(transition.Issue, out var existing))
            {
                if (!string.Equals(existing, transition.RoadmapItem, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Roadmap automation closure approval conflicts with the reconciliation manifest.");
                }

                continue;
            }

            if (transition.Issue < 1 || !itemIds.Add(transition.RoadmapItem))
            {
                throw new InvalidOperationException("Roadmap automation closure approval conflicts with the reconciliation manifest.");
            }

            transitions.Add(transition.Issue, transition.RoadmapItem);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (!string.Equals(property.Name, "closedItemTransitions", StringComparison.Ordinal))
                {
                    property.Value.WriteTo(writer);
                    continue;
                }

                writer.WriteStartArray();
                foreach (var transition in transitions.OrderBy(transition => transition.Key))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("issue", transition.Key);
                    writer.WriteString("roadmapItem", transition.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }
}
