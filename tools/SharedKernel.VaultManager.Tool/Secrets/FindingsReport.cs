using System.Globalization;
using System.Text;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Renders findings without values: item, kind, confidence, location, length, and target.</summary>
internal static class FindingsReport
{
    /// <summary>Renders a report.</summary>
    /// <param name="items">Findings per item.</param>
    /// <param name="format">Output format.</param>
    /// <returns>Report text.</returns>
    public static string Render(IEnumerable<ItemFindings> items, ReportFormat format)
    {
        ArgumentNullException.ThrowIfNull(items);
        var builder = new StringBuilder();
        if (format == ReportFormat.Markdown)
        {
            builder.Append("| Item | Kind | Confidence | Location | Length | Target | Status |\n");
            builder.Append("| --- | --- | --- | --- | --- | --- | --- |\n");
        }

        foreach (ItemFindings item in items)
        {
            foreach (SecretFinding finding in item.Findings)
            {
                string status = finding.NeedsReview ? "needs review" : finding.IsPreAccepted ? "pre-accepted" : "proposed";
                string[] cells =
                [
                    item.ItemName,
                    finding.Kind.ToString(),
                    finding.Confidence.ToString(),
                    finding.Location.ToString(),
                    finding.ValueLength.ToString(CultureInfo.InvariantCulture),
                    finding.ProposedTarget.ToString(),
                    status,
                ];
                if (format == ReportFormat.Markdown)
                {
                    builder.Append("| ").AppendJoin(" | ", cells.Select(Escape)).Append(" |\n");
                }
                else
                {
                    builder.AppendJoin("\t", cells.Select(cell => cell.Replace('\n', ' ').Replace('\t', ' '))).Append('\n');
                }
            }
        }

        return builder.ToString();
    }

    private static string Escape(string cell)
    {
        return cell.Replace("|", "\\|", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');
    }
}
