using System.Globalization;
using System.Text;
using PandaBear.VaultManager.Tool.Analysis;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Renders a pipeline result as one Markdown report. It holds names, keys, paths, and counts, never values.</summary>
internal static class VaultReport
{
    /// <summary>
    /// Renders the output status, the merge report, the secret findings with their decisions, placement problems,
    /// password health, the attachments to upload by hand, writer errors, and warnings.
    /// </summary>
    /// <param name="result">Pipeline result.</param>
    /// <returns>Markdown text with line feed line endings.</returns>
    public static string ToMarkdown(PipelineResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var builder = new StringBuilder();
        builder.Append("# Vault report\n\n").Append(OutputStatus(result)).Append("\n\n");
        AppendNested(builder, result.MergeReport.ToMarkdown());
        AppendFindings(builder, result.Findings);
        AppendPlacementProblems(builder, result.PlacementProblems);
        AppendPasswordHealth(builder, result.PasswordHealth);
        AppendAttachments(builder, result.ManualAttachments);
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

        if (result.OutputWritten)
        {
            return output.Plaintext
                ? $"The merged vault was written to `{output.Path}` as unencrypted JSON. Delete the file after the import."
                : $"The merged vault was written to `{output.Path}` as a password-protected export.";
        }

        return result.HasWriteErrors
            ? "The merged vault was not written because Bitwarden would reject the import; see the errors below."
            : string.Create(CultureInfo.InvariantCulture, $"The merged vault was not written because {result.Unresolved.Count} field(s) need a decision.");
    }

    // The merge report is a document of its own; one more heading level nests it under this report.
    private static void AppendNested(StringBuilder builder, string markdown)
    {
        foreach (string line in markdown.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'))
        {
            builder.Append(line.StartsWith('#') ? "#" + line : line).Append('\n');
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

    private static void AppendAttachments(StringBuilder builder, IReadOnlyList<ManualAttachment> attachments)
    {
        builder.Append("\n## Attachments to upload\n\n");
        if (attachments.Count == 0)
        {
            builder.Append("No item has attachments.\n");
            return;
        }

        builder.Append("Bitwarden JSON imports cannot carry files. After the import, upload each file to its item.\n\n")
            .Append("| Item | File | Source |\n| --- | --- | --- |\n");
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

    private static string Cell(string text) => OneLine(text).Replace("|", "\\|", StringComparison.Ordinal);

    private static string OneLine(string text) => text.Replace('\r', ' ').Replace('\n', ' ');
}
