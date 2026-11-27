using System.Globalization;
using System.Text.Json;

namespace SharedKernel.Repositories;

/// <summary>Locally reviewed planning inputs for a forge issue.</summary>
/// <param name="Order">The optional rank.</param>
/// <param name="Parent">The parent for a forge without parent relationships.</param>
/// <param name="Reach">The RICE reach.</param>
/// <param name="Impact">The RICE impact.</param>
/// <param name="Confidence">The RICE confidence.</param>
/// <param name="Effort">The RICE effort.</param>
internal sealed record ForgeRoadmapPriority(
    int? Order, ForgeIssueReference? Parent, decimal? Reach, decimal? Impact, decimal? Confidence, decimal? Effort)
{
    /// <summary>Parses a stable issue key.</summary>
    internal static ForgeIssueReference? ParseReference(string? key)
    {
        if (key is null)
        {
            return null;
        }

        var hash = key.IndexOf('#', StringComparison.Ordinal);
        if (hash < 0 || !long.TryParse(key.AsSpan(hash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number <= 0)
        {
            return null;
        }

        var repository = key[..hash];
        var parts = repository.Split('/');
        return parts.Length == 2 && parts.All(static part => part.Length > 0 && part is not "." and not ".."
            && part.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'))
            ? new ForgeIssueReference(repository, number)
            : null;
    }

    /// <summary>Formats the reference as a long-number stable issue key.</summary>
    internal static string Key(ForgeIssueReference reference) =>
        string.Create(CultureInfo.InvariantCulture, $"{reference.Repository}#{reference.Number}");

    /// <summary>Reads one priority entry, with optional local parent and all-or-none scoring.</summary>
    internal static ForgeRoadmapPriority? Parse(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int? order = null;
        if (entry.TryGetProperty("order", out var orderElement))
        {
            if (orderElement.ValueKind != JsonValueKind.Number || !orderElement.TryGetInt32(out var ordinal) || ordinal < 1)
            {
                return null;
            }

            order = ordinal;
        }

        ForgeIssueReference? parent = null;
        if (entry.TryGetProperty("parent", out var parentElement))
        {
            parent = parentElement.ValueKind == JsonValueKind.String ? ParseReference(parentElement.GetString()) : null;
            if (parent is null)
            {
                return null;
            }
        }

        if (!entry.TryGetProperty("scoring", out var scoring))
        {
            return new ForgeRoadmapPriority(order, parent, null, null, null, null);
        }

        if (scoring.ValueKind != JsonValueKind.Object
            || Number(scoring, "reach") is not >= 0
            || Number(scoring, "impact") is not (>= 1 and <= 5)
            || Number(scoring, "confidence") is not (>= 0.1m and <= 1)
            || Number(scoring, "effort") is not > 0)
        {
            return null;
        }

        var reach = Number(scoring, "reach");
        var impact = Number(scoring, "impact");
        var confidence = Number(scoring, "confidence");
        var effort = Number(scoring, "effort");
        try
        {
            _ = reach * impact * confidence / effort;
        }
        catch (OverflowException)
        {
            return null;
        }

        return new ForgeRoadmapPriority(order, parent, reach, impact, confidence, effort);
    }

    private static decimal? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)
            ? number
            : null;
}
