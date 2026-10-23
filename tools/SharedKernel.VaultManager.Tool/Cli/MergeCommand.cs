using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// <c>merge</c>: merges the inputs into one Bitwarden export, password-protected unless <c>--plaintext</c> is given. While
/// a field or a proposed finding needs a decision, it writes no vault, adds pending entries to the decisions file, and
/// exits with <see cref="ExitCodes.DecisionsNeeded"/>.
/// </summary>
internal static class MergeCommand
{
    private const string PreferPrefix = "prefer:";
    private const string OutputFile = "output file";
    private const string DecisionsFileName = "decisions file";

    /// <summary>Creates the command.</summary>
    /// <param name="environment">Password prompts, terminal state, and clock.</param>
    /// <returns>The command.</returns>
    public static Command Create(VaultCommandEnvironment environment)
    {
        Argument<string[]> inputs = CommandSymbols.Inputs();
        Option<string> output = CommandSymbols.FilePath(new Option<string>("--output", "-o")
        {
            Description = "File to write the merged vault to. It must not be an input; an existing file is replaced only with --force.",
            HelpName = "file",
            Required = true,
        });
        Option<string> decisions = CommandSymbols.FilePath(new Option<string>("--decisions")
        {
            Description = "Decisions file to read. When fields or findings need a decision, it is created or updated with pending entries to edit.",
            HelpName = "file",
        });
        var policy = new Option<string>("--policy")
        {
            Description = "How conflicting values are resolved: newest takes the most recently modified value, ask leaves every conflict to the decisions file, and prefer:<input file name> takes the value of that input.",
            HelpName = "newest|ask|prefer:<input file name>",
            DefaultValueFactory = static _ => "newest",
        };
        policy.Validators.Add(static result =>
        {
            string value = result.GetValueOrDefault<string>();
            if (ParsePolicy(value) is null)
            {
                result.AddError(PrintableText.OneLine($"--policy takes newest, ask, or prefer:<input file name>, not '{value}'."));
            }
        });
        var plaintext = new Option<bool>("--plaintext")
        {
            Description = "Write unencrypted Bitwarden JSON instead of a password-protected export. Delete the file after the import.",
        };
        Option<string> report = CommandSymbols.Report();
        var force = new Option<bool>("--force") { Description = "Replace an existing output file." };
        var command = new Command("merge", "Merge the inputs into one Bitwarden export, protected by a password unless --plaintext is given.")
        {
            inputs,
            output,
            decisions,
            policy,
            plaintext,
            report,
            force,
        };
        command.Validators.Add(result => ValidatePreferredInput(result, inputs, policy));
        CommandAction.Set(command, environment, (parseResult, prompts, ct) => Run(
            new MergeArguments(
                parseResult.GetRequiredValue(inputs),
                parseResult.GetRequiredValue(output),
                parseResult.GetValue(decisions),
                ParsePolicy(parseResult.GetRequiredValue(policy))!,
                parseResult.GetValue(plaintext),
                parseResult.GetValue(report),
                parseResult.GetValue(force)),
            parseResult.InvocationConfiguration,
            prompts,
            ct));
        return command;
    }

    private static MergePolicy? ParsePolicy(string value)
    {
        if (string.Equals(value, "newest", StringComparison.Ordinal))
        {
            return new MergePolicy();
        }

        if (string.Equals(value, "ask", StringComparison.Ordinal))
        {
            return new MergePolicy(MergePolicyKind.Ask);
        }

        if (value.StartsWith(PreferPrefix, StringComparison.Ordinal) && value.Length > PreferPrefix.Length)
        {
            return new MergePolicy(MergePolicyKind.PreferSource, value[PreferPrefix.Length..]);
        }

        return null;
    }

    // The preferred source is matched against the file name each item was read from, so it must name exactly one input.
    // The inputs are read from their tokens, because reading a value that failed to parse throws.
    private static void ValidatePreferredInput(CommandResult result, Argument<string[]> inputs, Option<string> policy)
    {
        if (ParsePolicy(result.GetValue(policy) ?? string.Empty) is not { Kind: MergePolicyKind.PreferSource, PreferredSourceFile: { } preferred })
        {
            return;
        }

        string[] names = [.. (result.GetResult(inputs)?.Tokens ?? []).Select(token => Path.GetFileName(token.Value))];
        int matches = names.Count(name => string.Equals(name, preferred, StringComparison.Ordinal));
        if (matches == 0)
        {
            result.AddError(PrintableText.OneLine($"--policy prefer:{preferred} names no input file. Input file names: {string.Join(", ", names)}."));
        }
        else if (matches > 1)
        {
            result.AddError(PrintableText.OneLine($"--policy prefer:{preferred} names several input files. Give each input a different file name."));
        }
    }

    private static int Run(MergeArguments arguments, InvocationConfiguration invocation, PasswordPrompts prompts, CancellationToken ct)
    {
        TextWriter output = invocation.Output;
        TextWriter error = invocation.Error;
        if (!CanWriteFiles(arguments, error))
        {
            return ExitCodes.InputOutputProblem;
        }

        VaultFileFormat?[] formats = InputChecks.Detect(arguments.Inputs, error);
        if (Array.IndexOf(formats, null) >= 0 || !PasswordsCanBeAsked(arguments, formats, prompts.IsInteractive, error))
        {
            return ExitCodes.InputOutputProblem;
        }

        MergeDecisions decisions = arguments.Decisions is { } decisionsPath ? DecisionsFile.Read(decisionsPath) : MergeDecisions.None;
        var pipeline = new VaultPipeline(prompts.Environment);
        ScannedVault scanned = VaultPipeline.Scan(pipeline.Load(arguments.Inputs, ct), ct);
        var request = new MergeRequest { Policy = arguments.Policy, Decisions = decisions };

        // The first merge requests no output, so the new export password is asked only when the vault will be written. The
        // second merge writes the vault once nothing is pending. Both merge the same scanned vault, so each input is read
        // and each password asked once.
        PipelineResult result = pipeline.Merge(scanned, request, ct);
        if (PendingDecisions.Find(result, decisions).IsEmpty && !result.HasWriteErrors)
        {
            result = pipeline.Merge(scanned, request with { Output = RequestedOutput(arguments) }, ct);
        }

        return Finish(arguments, decisions, result, output, error, ct);
    }

    private static bool CanWriteFiles(MergeArguments arguments, TextWriter error)
    {
        (string Path, string Name) mergedVault = (arguments.Output, OutputFile);
        bool canWrite = OutputFiles.CanWrite(arguments.Output, OutputFile, arguments.Inputs, [], arguments.Force, error);
        if (arguments.Decisions is { } decisionsPath)
        {
            canWrite &= OutputFiles.CanWrite(decisionsPath, DecisionsFileName, arguments.Inputs, [mergedVault], replaceExisting: true, error);
        }

        if (arguments.Report is { } reportPath)
        {
            (string Path, string Name)[] others = arguments.Decisions is { } decisionsFile ? [mergedVault, (decisionsFile, DecisionsFileName)] : [mergedVault];
            canWrite &= OutputFiles.CanWriteReport(reportPath, arguments.Inputs, others, error);
        }

        return canWrite;
    }

    private static bool PasswordsCanBeAsked(MergeArguments arguments, IReadOnlyList<VaultFileFormat?> formats, bool isInteractive, TextWriter error)
    {
        bool canAsk = InputChecks.PasswordsCanBeAsked(arguments.Inputs, formats, isInteractive, error);
        if (isInteractive || arguments.Plaintext)
        {
            return canAsk;
        }

        error.WriteLine("The merged vault is written as a password-protected export, and standard input is not a terminal to type its password in. Run the command in a terminal, or pass --plaintext to write unencrypted JSON.");
        return false;
    }

    private static int Finish(MergeArguments arguments, MergeDecisions decisions, PipelineResult result, TextWriter output, TextWriter error, CancellationToken ct)
    {
        foreach (string warning in result.ReadWarnings.Concat(result.WriteWarnings).Concat(UnmatchedDecisions.Find(decisions, result)))
        {
            error.WriteLine("Warning: " + PrintableText.OneLine(warning));
        }

        PendingDecisions pending = result.OutputWritten ? PendingDecisions.None : PendingDecisions.Find(result, decisions);
        if (!pending.IsEmpty && arguments.Decisions is { } decisionsPath)
        {
            ct.ThrowIfCancellationRequested();
            DecisionsFile.Write(decisionsPath, decisions.WithPending(pending.Fields, pending.Findings));
        }

        WriteSummary(output, result, pending);
        int exitCode;
        if (result.OutputWritten)
        {
            // Said before the report is written, so a report that cannot be written never hides the written vault.
            string outputPath = PrintableText.OneLine(arguments.Output);
            output.WriteLine(arguments.Plaintext
                ? $"Wrote the merged vault to {outputPath} as unencrypted JSON. Delete it after the import."
                : $"Wrote the merged vault to {outputPath} as a password-protected export.");
            exitCode = ExitCodes.Success;
        }
        else
        {
            exitCode = NotWritten(arguments, result, pending, error);
        }

        if (arguments.Report is { } reportPath)
        {
            try
            {
                SecureFile.WriteAllText(reportPath, Report(result with { Output = RequestedOutput(arguments) }, pending), overwrite: true, ct);
            }
            catch (Exception exception) when (result.OutputWritten && CommandAction.IsFileProblem(exception))
            {
                error.WriteLine(PrintableText.OneLine($"The merged vault was written to {arguments.Output}, but the report was not written: {exception.Message}"));
                return ExitCodes.InputOutputProblem;
            }

            output.WriteLine("Report: " + PrintableText.OneLine(reportPath));
        }

        return exitCode;
    }

    // Says why the merged vault is not written and returns the exit code. Writer errors decide it: no decision fixes
    // them, and fixing an input can change what needs a decision.
    private static int NotWritten(MergeArguments arguments, PipelineResult result, PendingDecisions pending, TextWriter error)
    {
        foreach (string writeError in result.WriteErrors)
        {
            error.WriteLine("Error: " + PrintableText.OneLine(writeError));
        }

        if (!pending.IsEmpty)
        {
            error.WriteLine(PendingMessage(pending, arguments.Decisions));
        }

        error.WriteLine(result.HasWriteErrors ? "The merged vault was not written because Bitwarden would reject the import." : "The merged vault was not written.");
        return result.HasWriteErrors ? ExitCodes.InputOutputProblem : ExitCodes.DecisionsNeeded;
    }

    private static PipelineOutput RequestedOutput(MergeArguments arguments) => new(arguments.Output, arguments.Plaintext, arguments.Force);

    private static void WriteSummary(TextWriter output, PipelineResult result, PendingDecisions pending)
    {
        CommandText.Count(output, "Items in the merged vault", result.Merged.Items.Count);
        CommandText.Count(output, "Merged groups", result.MergeReport.MergedItems);
        CommandText.Count(output, "Conflicts resolved by the policy", result.MergeReport.AutoResolvedConflicts);
        CommandText.Count(output, "Fields that need a decision", pending.Fields.Count);
        CommandText.Count(output, "Secret findings", result.Findings.Sum(item => item.Findings.Count));
        CommandText.Count(output, "  Applied", result.Findings.Sum(item => item.AcceptedFindingIds.Count));
        CommandText.Count(output, "  Need a decision", pending.FindingCount);
        CommandText.Count(output, "Password issues", result.PasswordHealth.Issues.Count);
        CommandText.Count(output, "Attachments to upload after the import", result.AttachmentUploadCount);
    }

    private static string PendingMessage(PendingDecisions pending, string? decisionsPath)
    {
        string counts = string.Create(CultureInfo.InvariantCulture, $"{pending.Fields.Count} field(s) and {pending.FindingCount} finding(s) need a decision.");
        return decisionsPath is null
            ? counts + " Run the merge again with --decisions <file> to write a pending entry for each to that file."
            : counts + " Pending entries are in " + PrintableText.OneLine(decisionsPath) + "; replace each with a decision and run the merge again.";
    }

    private static string Report(PipelineResult result, PendingDecisions pending)
    {
        string markdown = VaultReport.ToMarkdown(result);
        if (pending.IsEmpty)
        {
            return markdown;
        }

        var builder = new StringBuilder(markdown);
        builder.Append("\n## Decisions needed\n\n")
            .Append("The merged vault is not written until these fields and findings are decided in the decisions file given with --decisions.\n");
        if (pending.Fields.Count > 0)
        {
            builder.Append("\n| Group | Field | Members |\n| --- | --- | --- |\n");
            foreach (UnresolvedConflict conflict in pending.Fields)
            {
                builder.Append("| `").Append(Cell(conflict.GroupKey)).Append("` | `").Append(Cell(conflict.FieldPath)).Append("` | ")
                    .AppendJoin(", ", conflict.MemberOriginKeys.Select((originKey, index) => "`" + Cell(originKey) + "` (" + Cell(conflict.MemberNames[index]) + ")"))
                    .Append(" |\n");
            }
        }

        if (pending.FindingCount > 0)
        {
            builder.Append("\n| Item | Origin key | Finding |\n| --- | --- | --- |\n");
            foreach (ItemFindingDecisions item in pending.Findings)
            {
                foreach (FindingChoice choice in item.Choices)
                {
                    builder.Append("| ").Append(Cell(item.ItemName ?? string.Empty))
                        .Append(" | `").Append(Cell(item.OriginKey))
                        .Append("` | `").Append(Cell(choice.FindingId)).Append("` |\n");
                }
            }
        }

        return builder.ToString();
    }

    private static string Cell(string text) => PrintableText.TableCell(text);
}
