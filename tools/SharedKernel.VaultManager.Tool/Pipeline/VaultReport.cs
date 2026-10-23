using System.Globalization;
using System.Text;
using PandaBear.VaultManager.Tool.Analysis;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>
/// Renders a pipeline result as one Markdown report. It holds names, keys, paths, and counts, never values. Every name,
/// path, and message is printed on one line with every control character made printable by <see cref="PrintableText"/>,
/// so the report can be shown on a terminal.
/// </summary>
internal static class VaultReport
{
    /// <summary>
    /// Renders the output status, the merge report, the secret findings with their decisions, placement problems,
    /// password health, the attachments to upload by hand, writer errors, and warnings.
    /// </summary>
    /// <param name="result">Pipeline result.</param>
    /// <returns>Markdown text with line feed line endings and no other control character.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static string ToMarkdown(PipelineResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var builder = new StringBuilder();
        builder.Append("# Vault report\n\n").Append(OutputStatus(result)).Append("\n\n");
        AppendNested(builder, result.MergeReport.ToMarkdown());
        AppendFindings(builder, result.Findings);
        AppendPlacementProblems(builder, result.PlacementProblems);
        AppendPasswordHealth(builder, result.PasswordHealth);
        AppendAttachments(builder, result.ManualAttachments, result.UnassignedAttachmentFolders);
        AppendList(builder, "Errors", result.WriteErrors);
        AppendList(builder, "Warnings", [.. result.ReadWarnings, .. result.WriteWarnings]);
        return builder.ToString();
    }

    private static string OutputStatus(PipelineResult result)
    {
        if (result.Output is not { } output)
        {
            return "No output file was requested.";
        }

        string path = PrintableText.OneLine(output.Path);
        if (result.OutputWritten)
        {
            return output.Plaintext
                ? $"The merged vault was written to `{path}` as unencrypted JSON. Delete the file after the import."
                : $"The merged vault was written to `{path}` as a password-protected export.";
        }

        if (result.HasWriteErrors)
        {
            return "The merged vault was not written because Bitwarden would reject the import; see the errors below.";
        }

        if (result.PendingFindingCount > 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"The merged vault was not written because {result.Unresolved.Count} field(s) and {result.PendingFindingCount} finding(s) need a decision.");
        }

        return result.HasUnresolvedConflicts
            ? string.Create(CultureInfo.InvariantCulture, $"The merged vault was not written because {result.Unresolved.Count} field(s) need a decision.")
            : "The merged vault was not written.";
    }

    // The merge report is a document of its own; one more heading level nests it under this report. Its lines end with the
    // platform's line ending, so only "\r\n" and "\n" end a line here; any other line break character stays inside its
    // line, where PrintableText makes it printable.
    private static void AppendNested(StringBuilder builder, string markdown)
    {
        foreach (string line in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'))
        {
            builder.Append(line.StartsWith('#') ? "#" : string.Empty).Append(PrintableText.OneLine(line)).Append('\n');
        }
    }

    private static void AppendFindings(StringBuilder builder, IReadOnlyList<ItemFindingsResult> findings)
    {
        builder.Append("\n## Secret findings\n\n");
        if (findings.Count == 0)
        {
            builder.Append("No secrets were found outside their fields.\n");
            return;
        }

        int total = findings.Sum(item => item.Findings.Count);
        int accepted = findings.Sum(item => item.AcceptedFindingIds.Count);
        builder.Append(CultureInfo.InvariantCulture, $"{accepted} of {total} finding(s) were accepted. ")
            .Append("Pre-accepted findings are placed unless the decisions file rejects them; other findings only when it accepts them.\n\n");
        builder.Append(FindingsReport.Render([.. findings.Select(item => new ItemFindings(item.ItemName, item.Findings))], ReportFormat.Markdown));
        builder.Append("\n| Item | Origin key | Finding | Accepted |\n| --- | --- | --- | --- |\n");
        foreach (ItemFindingsResult item in findings)
        {
            foreach (SecretFinding finding in item.Findings)
            {
                bool isAccepted = item.AcceptedFindingIds.Contains(finding.Id, StringComparer.Ordinal);
                builder.Append("| ").Append(Cell(item.ItemName))
                    .Append(" | `").Append(Cell(item.OriginKey))
                    .Append("` | `").Append(Cell(finding.Id))
                    .Append("` | ").Append(isAccepted ? "yes" : "no").Append(" |\n");
            }
        }
    }

    private static void AppendPlacementProblems(StringBuilder builder, IReadOnlyList<PlacementProblem> problems)
    {
        if (problems.Count == 0)
        {
            return;
        }

        builder.Append("\n## Placement problems\n\n| Item | Problem |\n| --- | --- |\n");
        foreach (PlacementProblem problem in problems)
        {
            builder.Append("| ").Append(Cell(problem.ItemName)).Append(" | ").Append(Cell(problem.Problem)).Append(" |\n");
        }
    }

    private static void AppendPasswordHealth(StringBuilder builder, PasswordHealthReport health)
    {
        builder.Append("\n## Password health\n\n");
        if (health.Issues.Count == 0)
        {
            builder.Append("No reused, short, or easily guessed passwords were found.\n");
            return;
        }

        builder.Append("| Item | Issue | Also used by |\n| --- | --- | --- |\n");
        foreach (PasswordIssue issue in health.Issues)
        {
            builder.Append("| ").Append(Cell(issue.ItemName))
                .Append(" | ").Append(Describe(issue.Kind))
                .Append(" | ").Append(Cell(string.Join(", ", issue.RelatedItemNames))).Append(" |\n");
        }
    }

    private static void AppendAttachments(StringBuilder builder, IReadOnlyList<ManualAttachment> attachments, IReadOnlyList<UnassignedAttachmentFolder> folders)
    {
        builder.Append("\n## Attachments to upload\n\n");
        if (attachments.Count == 0 && folders.Count == 0)
        {
            builder.Append("No item has attachments.\n");
            return;
        }

        builder.Append("Bitwarden JSON imports cannot carry files. After the import, upload each file to its item.\n");
        if (attachments.Count > 0)
        {
            builder.Append("\n| Item | File | Source |\n| --- | --- | --- |\n");
            foreach (ManualAttachment attachment in attachments)
            {
                string source = attachment.ArchivePath is { } archivePath
                    ? Cell(attachment.SourceFile) + ", `" + Cell(archivePath) + "`"
                    : Cell(attachment.SourceFile) + "; not included in the export, download it from the source vault";
                builder.Append("| ").Append(Cell(attachment.ItemName))
                    .Append(" | ").Append(Cell(attachment.FileName))
                    .Append(" | ").Append(source).Append(" |\n");
            }
        }

        if (folders.Count > 0)
        {
            builder.Append("\nA Bitwarden zip export names the attachment folder of each item after the item, adding `_1`, `_2`, and so on ")
                .Append("for items with the same name. These folders match no item, or several items, so the item each of their files ")
                .Append("belongs to is unknown. Open the items with that name and upload each file to the one it belongs to.\n\n")
                .Append("| Folder | File | Source | Matching items |\n| --- | --- | --- | --- |\n");
            foreach (UnassignedAttachmentFolder folder in folders)
            {
                foreach (AttachmentRef file in folder.Files)
                {
                    builder.Append("| ").Append(Cell(folder.FolderName))
                        .Append(" | ").Append(Cell(file.FileName))
                        .Append(" | ").Append(Cell(folder.SourceFile)).Append(", `").Append(Cell(file.SourcePath ?? file.FileName)).Append('`')
                        .Append(" | ").Append(folder.MatchingItemCount.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
                }
            }
        }
    }

    private static void AppendList(StringBuilder builder, string heading, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        builder.Append("\n## ").Append(heading).Append("\n\n");
        foreach (string line in lines)
        {
            builder.Append("- ").Append(OneLine(line)).Append('\n');
        }
    }

    private static string Describe(PasswordIssueKind kind) => kind switch
    {
        PasswordIssueKind.Reused => "reused on other sites",
        PasswordIssueKind.TooShort => string.Create(CultureInfo.InvariantCulture, $"shorter than {PasswordHealthReport.MinimumLength} characters"),
        PasswordIssueKind.SingleCharacterClass => "only one kind of character",
        PasswordIssueKind.EasilyGuessed => "easily guessed",
        _ => kind.ToString(),
    };

    private static string Cell(string text) => PrintableText.TableCell(text);

    private static string OneLine(string text) => PrintableText.OneLine(text);
}
