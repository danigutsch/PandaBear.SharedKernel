using System.CommandLine;
using PandaBear.VaultManager.Tool.Analysis;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// <c>scan</c>: finds secrets stored outside their fields and weak or reused passwords. It writes a value-free summary to
/// standard output and, with <c>--report</c>, the full Markdown report.
/// </summary>
internal static class ScanCommand
{
    /// <summary>Creates the command.</summary>
    /// <param name="environment">Password prompts, terminal state, and clock.</param>
    /// <returns>The command.</returns>
    public static Command Create(VaultCommandEnvironment environment)
    {
        Argument<string[]> inputs = CommandSymbols.Inputs();
        Option<string> report = CommandSymbols.Report();
        var command = new Command("scan", "Report secret findings and password health without vault values.")
        {
            inputs,
            report,
        };
        CommandAction.Set(command, environment, (parseResult, prompts, ct) =>
            Run(parseResult.GetRequiredValue(inputs), parseResult.GetValue(report), parseResult.InvocationConfiguration, prompts, ct));
        return command;
    }

    private static int Run(string[] inputs, string? reportPath, InvocationConfiguration invocation, PasswordPrompts prompts, CancellationToken ct)
    {
        TextWriter output = invocation.Output;
        TextWriter error = invocation.Error;
        if (reportPath is not null && !OutputFiles.CanWriteReport(reportPath, inputs, [], error))
        {
            return ExitCodes.InputOutputProblem;
        }

        VaultFileFormat?[] formats = InputChecks.Detect(inputs, error);
        if (Array.IndexOf(formats, null) >= 0 || !InputChecks.PasswordsCanBeAsked(inputs, formats, prompts.IsInteractive, error))
        {
            return ExitCodes.InputOutputProblem;
        }

        var pipeline = new VaultPipeline(prompts.Environment);
        PipelineResult result = pipeline.Merge(VaultPipeline.Scan(pipeline.Load(inputs, ct), ct), new MergeRequest(), ct);
        foreach (string warning in result.ReadWarnings)
        {
            error.WriteLine("Warning: " + PrintableText.OneLine(warning));
        }

        WriteSummary(output, result);
        if (reportPath is not null)
        {
            SecureFile.WriteAllText(reportPath, VaultReport.ToMarkdown(result), overwrite: true, ct);
            output.WriteLine("Report: " + PrintableText.OneLine(reportPath));
        }

        return ExitCodes.Success;
    }

    private static void WriteSummary(TextWriter output, PipelineResult result)
    {
        var findings = result.Findings.SelectMany(item => item.Findings).ToArray();
        CommandText.Count(output, "Items after merging duplicates", result.Merged.Items.Count);
        CommandText.Count(output, "Secret findings", findings.Length);
        CommandText.Count(output, "  Pre-accepted", findings.Count(finding => finding.IsPreAccepted));
        CommandText.Count(output, "  Proposed", findings.Count(finding => !finding.IsPreAccepted && !finding.NeedsReview));
        CommandText.Count(output, "  Needs review", findings.Count(finding => finding.NeedsReview));
        CommandText.Count(output, "Items with findings", result.Findings.Count);
        CommandText.Count(output, "Password issues", result.PasswordHealth.Issues.Count);
        foreach (IGrouping<PasswordIssueKind, PasswordIssue> kind in result.PasswordHealth.Issues.GroupBy(issue => issue.Kind).OrderBy(kind => kind.Key))
        {
            CommandText.Count(output, "  " + Describe(kind.Key), kind.Count());
        }

        CommandText.Count(output, "Attachments to upload after the import", result.AttachmentUploadCount);
    }

    private static string Describe(PasswordIssueKind kind) => kind switch
    {
        PasswordIssueKind.Reused => "Reused on other sites",
        PasswordIssueKind.Weak => "Weak",
        _ => kind.ToString(),
    };
}
