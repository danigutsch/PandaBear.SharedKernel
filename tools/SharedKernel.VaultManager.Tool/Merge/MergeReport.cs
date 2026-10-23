using System.Globalization;
using System.Text;
using PandaBear.VaultManager.Tool.IO;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Value-free summary of a merge. It holds counts, item names, field paths, and resolutions only.</summary>
internal sealed record MergeReport
{
    /// <summary>Groups with one member.</summary>
    public int UniqueGroups { get; init; }

    /// <summary>Groups whose members are equal after normalization.</summary>
    public int IdenticalGroups { get; init; }

    /// <summary>Groups whose members differ.</summary>
    public int DifferentGroups { get; init; }

    /// <summary>Output items produced from groups with more than one member.</summary>
    public int MergedItems { get; init; }

    /// <summary>Conflicts the policy resolved without a decision.</summary>
    public int AutoResolvedConflicts { get; init; }

    /// <summary>Conflicts that need a decision.</summary>
    public int UnresolvedConflicts { get; init; }

    /// <summary>Groups with changed fields.</summary>
    public IReadOnlyList<GroupReport> Groups { get; init; } = [];

    /// <summary>Conversion notes of the output items, prefixed with the item name.</summary>
    public IReadOnlyList<string> ConversionNotes { get; init; } = [];

    /// <summary>
    /// Renders the report as Markdown. The output contains no values, and item names and conversion notes are printed on
    /// one line with every control character made printable by <see cref="PrintableText"/>.
    /// </summary>
    /// <returns>Markdown text.</returns>
    public string ToMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Merge report").AppendLine();
        builder.AppendLine("| Groups | Count |").AppendLine("| --- | ---: |");
        Row(builder, "Unique", UniqueGroups);
        Row(builder, "Identical", IdenticalGroups);
        Row(builder, "Different", DifferentGroups);
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Merged items: {MergedItems}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Auto-resolved conflicts: {AutoResolvedConflicts}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Unresolved conflicts: {UnresolvedConflicts}");

        if (Groups.Count > 0)
        {
            builder.AppendLine().AppendLine("## Changed groups");
            foreach (var group in Groups)
            {
                builder.AppendLine()
                    .Append("### ").Append(Escape(string.Join(" / ", group.MemberNames.Distinct(StringComparer.Ordinal))))
                    .Append(" (`").Append(group.GroupKey).AppendLine("`)").AppendLine();
                builder.AppendLine("| Field | Resolution |").AppendLine("| --- | --- |");
                foreach (var field in group.Fields)
                {
                    builder.Append("| `").Append(field.FieldPath).Append("` | ").Append(Describe(field.Resolution)).AppendLine(" |");
                }
            }
        }

        if (ConversionNotes.Count > 0)
        {
            builder.AppendLine().AppendLine("## Conversion notes").AppendLine();
            foreach (string note in ConversionNotes)
            {
                builder.Append("- ").AppendLine(Escape(note));
            }
        }

        return builder.ToString();
    }

    private static void Row(StringBuilder builder, string label, int count) =>
        builder.AppendLine(CultureInfo.InvariantCulture, $"| {label} | {count} |");

    private static string Describe(ResolutionKind resolution) => resolution switch
    {
        ResolutionKind.Equal => "equal",
        ResolutionKind.OneSided => "one-sided",
        ResolutionKind.Union => "union",
        ResolutionKind.Newest => "newest",
        ResolutionKind.PreferredSource => "preferred source",
        ResolutionKind.Decision => "decision",
        ResolutionKind.Unresolved => "unresolved",
        _ => resolution.ToString(),
    };

    private static string Escape(string text) => PrintableText.TableCell(text);
}
