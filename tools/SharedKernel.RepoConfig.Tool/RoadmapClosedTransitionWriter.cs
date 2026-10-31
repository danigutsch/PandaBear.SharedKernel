using System.Text;
using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Writes approved closed-item transitions into the roadmap reconciliation manifest.</summary>
internal static class RoadmapClosedTransitionWriter
{
    /// <summary>Adds approved transitions to the manifest's closedItemTransitions array, rejecting invalid or conflicting entries, and returns the updated manifest text.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="manifestContent"/> or <paramref name="approvedTransitions"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The manifest is invalid, or an approved transition conflicts with it.</exception>
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

        Dictionary<int, (string RoadmapItem, string CloseReason)> transitions = [];
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
                var closeReason = RoadmapCloseReason.Completed;
                if (entry.TryGetProperty("closeReason", out var reasonElement))
                {
                    closeReason = reasonElement.ValueKind == JsonValueKind.String ? reasonElement.GetString() ?? string.Empty : string.Empty;
                    if (!string.Equals(closeReason, RoadmapCloseReason.NotPlanned, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Roadmap reconciliation manifest closed item transitions are invalid.");
                    }
                }

                if (!transitions.TryAdd(issue, (roadmapItem, closeReason)) || !itemIds.Add(roadmapItem))
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
                if (!string.Equals(existing.RoadmapItem, transition.RoadmapItem, StringComparison.Ordinal)
                    || !string.Equals(existing.CloseReason, transition.CloseReason, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Roadmap automation closure approval conflicts with the reconciliation manifest.");
                }

                continue;
            }

            if (transition.Issue < 1 || !itemIds.Add(transition.RoadmapItem))
            {
                throw new InvalidOperationException("Roadmap automation closure approval conflicts with the reconciliation manifest.");
            }

            if (!RoadmapCloseReason.IsValid(transition.CloseReason))
            {
                throw new InvalidOperationException("Roadmap automation closure approval conflicts with the reconciliation manifest.");
            }

            transitions.Add(transition.Issue, (transition.RoadmapItem, transition.CloseReason));
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
                    writer.WriteString("roadmapItem", transition.Value.RoadmapItem);
                    if (!string.Equals(transition.Value.CloseReason, RoadmapCloseReason.Completed, StringComparison.Ordinal))
                    {
                        writer.WriteString("closeReason", transition.Value.CloseReason);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }
}
