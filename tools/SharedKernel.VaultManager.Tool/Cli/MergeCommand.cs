using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text;
using PandaBear.VaultManager.Tool.Brief;
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
    private const string ReportFileName = "report file";
    private const string BriefFileName = "brief file";
    private const string AgentDecisionsFileName = "agent decisions file";

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
        Option<string> brief = CommandSymbols.FilePath(new Option<string>("--brief")
        {
            Description = "Write the brief: a JSON file without secret values that lists what needs a decision, for an agent that proposes decisions. An existing brief is replaced; any other existing file and an input never are. With --agent-decisions, the brief the agent read, which the merge checks instead of writing.",
            HelpName = "file",
        });
        Option<string> agentDecisions = CommandSymbols.FilePath(new Option<string>("--agent-decisions")
        {
            Description = "Decisions file that an agent wrote from the brief given with --brief. It may only take a member's value for a field the brief lets an agent decide and accept or reject the findings the brief lists; the decisions file wins on the same field or finding.",
            HelpName = "file",
        });
        var command = new Command("merge", "Merge the inputs into one Bitwarden export, protected by a password unless --plaintext is given.")
        {
            inputs,
            output,
            decisions,
            policy,
            plaintext,
            report,
            force,
            brief,
            agentDecisions,
        };
        command.Validators.Add(result => ValidatePreferredInput(result, inputs, policy));
        command.Validators.Add(result =>
        {
            if (result.GetResult(agentDecisions) is not null && result.GetResult(brief) is null)
            {
                result.AddError("--agent-decisions needs --brief <file>: the brief the agent read.");
            }
        });
        CommandAction.Set(command, environment, (parseResult, prompts, ct) => Run(
            new MergeArguments(
                parseResult.GetRequiredValue(inputs),
                parseResult.GetRequiredValue(output),
                parseResult.GetValue(decisions),
                ParsePolicy(parseResult.GetRequiredValue(policy))!,
                parseResult.GetValue(plaintext),
                parseResult.GetValue(report),
                parseResult.GetValue(force),
                parseResult.GetValue(brief),
                parseResult.GetValue(agentDecisions)),
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
        (MergeDecisions Decisions, BriefDocument Brief)? agent = null;
        if (arguments.AgentDecisions is { } agentPath)
        {
            // Read once, before any password is asked, so the files checked are the files merged.
            var fromAgent = (Decisions: ReadAgentDecisions(agentPath), Brief: BriefFile.Read(arguments.Brief!));
            bool sameInputs = SameInputs(arguments, fromAgent.Brief, error);
            if (!Report(agentPath, AgentDecisions.Check(fromAgent.Decisions, fromAgent.Brief), "decides what an agent may not", error) || !sameInputs)
            {
                return ExitCodes.InputOutputProblem;
            }

            agent = fromAgent;
        }

        var pipeline = new VaultPipeline(prompts.Environment);
        ScannedVault scanned = VaultPipeline.Scan(pipeline.Load(arguments.Inputs, ct), ct);
        var request = new MergeRequest { Policy = arguments.Policy, Decisions = decisions };

        // The first merge requests no output, so the new export password is asked only when the vault will be written. The
        // last merge writes the vault once nothing is pending. Every merge uses the same scanned vault, so each input is read
        // and each password asked once.
        PipelineResult result = pipeline.Merge(scanned, request, ct);
        if (agent is { } fromBrief)
        {
            // The agent's decisions are checked again against the inputs as read, with every group listed, so a brief edited
            // after it was written cannot widen the limits, and decisions that no longer fit stop the merge. Decisions the
            // user made since the brief was written may take a group out of the brief; the user's decision wins there.
            BriefDocument current = BriefBuilder.Build(fromBrief.Brief.Inputs, scanned, result, decisions, everyGroup: true);
            if (!Report(arguments.AgentDecisions!, AgentDecisions.Check(fromBrief.Decisions, current), "no longer fits these inputs and decisions. Write a new brief with merge --brief and let the agent decide from it", error))
            {
                return ExitCodes.InputOutputProblem;
            }

            PipelineResult userOnly = result;
            request = request with { Decisions = AgentDecisions.Combine(decisions, fromBrief.Decisions) };
            result = pipeline.Merge(scanned, request, ct);
            if (!Report(arguments.AgentDecisions!, AgentDecisions.Conflicts(userOnly, result, request.Decisions), "changes what the brief describes", error))
            {
                return ExitCodes.InputOutputProblem;
            }
        }
        else if (arguments.Brief is { } briefPath)
        {
            // Written before the vault, so a brief that cannot be written stops the merge before anything else is written.
            ct.ThrowIfCancellationRequested();
            BriefFile.Write(briefPath, BriefBuilder.Build(BriefInputs(arguments), scanned, result, decisions), ct);
        }

        if (PendingDecisions.Find(result, request.Decisions).IsEmpty && !result.HasWriteErrors)
        {
            result = pipeline.Merge(scanned, request with { Output = RequestedOutput(arguments) }, ct);
        }

        return Finish(arguments, decisions, agent?.Decisions, result, output, error, ct);
    }

    private static string[] BriefInputs(MergeArguments arguments) => [.. arguments.Inputs.Select(Path.GetFileName).OfType<string>()];

    private static MergeDecisions ReadAgentDecisions(string path) =>
        File.Exists(path)
            ? DecisionsFile.Read(path)
            : throw new FileNotFoundException($"The {AgentDecisionsFileName} '{Path.GetFullPath(path)}' does not exist.");

    // The brief names each input, so a brief of other files is refused before any password is asked. It holds no hash of
    // them, so a brief of another version of a file is caught after the inputs are read, when its groups, fields, and
    // findings no longer fit.
    private static bool SameInputs(MergeArguments arguments, BriefDocument brief, TextWriter error)
    {
        string[] inputs = BriefInputs(arguments);
        if (inputs.SequenceEqual(brief.Inputs, StringComparer.Ordinal))
        {
            return true;
        }

        string briefPath = PrintableText.OneLine(arguments.Brief!);
        error.WriteLine(PrintableText.OneLine($"Error: {briefPath} was made from {string.Join(", ", brief.Inputs)}, not from {string.Join(", ", inputs)}."));
        error.WriteLine("The merge stopped because the brief is not the brief of these inputs. Write a new brief with merge --brief and let the agent decide from it. Nothing was written.");
        return false;
    }

    // Prints each problem of the agent decisions file and says why the merge stopped.
    private static bool Report(string agentPath, IReadOnlyList<string> problems, string reason, TextWriter error)
    {
        string fileName = Path.GetFileName(agentPath);
        foreach (string problem in problems)
        {
            error.WriteLine("Error: " + PrintableText.OneLine(fileName + ": " + problem));
        }

        if (problems.Count > 0)
        {
            error.WriteLine(PrintableText.OneLine($"The merge stopped because the agent decisions file {reason}. Nothing was written."));
        }

        return problems.Count == 0;
    }

    // Each file is checked against the inputs and the files checked before it, so no two of them are one file.
    private static bool CanWriteFiles(MergeArguments arguments, TextWriter error)
    {
        // With agent decisions, the brief and the agent's file are read, never written.
        List<(string Path, string Name)> written = arguments.AgentDecisions is { } agentPath
            ? [(agentPath, AgentDecisionsFileName), (arguments.Brief!, BriefFileName)]
            : [];
        bool canWrite = OutputFiles.CanWrite(arguments.Output, OutputFile, arguments.Inputs, [.. written], arguments.Force, error);
        written.Add((arguments.Output, OutputFile));
        if (arguments.Decisions is { } decisionsPath)
        {
            canWrite &= OutputFiles.CanWrite(decisionsPath, DecisionsFileName, arguments.Inputs, [.. written], replaceExisting: true, error);
            written.Add((decisionsPath, DecisionsFileName));
        }

        if (arguments.Report is { } reportPath)
        {
            canWrite &= OutputFiles.CanWriteReport(reportPath, arguments.Inputs, [.. written], error);
            written.Add((reportPath, ReportFileName));
        }

        if (arguments.Brief is { } briefPath && arguments.AgentDecisions is null)
        {
            canWrite &= OutputFiles.CanWriteBrief(briefPath, arguments.Inputs, [.. written], error);
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

    // The decisions file gets pending entries for what the user's and the agent's decisions leave undecided. The agent's
    // decisions are never copied into it.
    private static int Finish(MergeArguments arguments, MergeDecisions decisions, MergeDecisions? agent, PipelineResult result, TextWriter output, TextWriter error, CancellationToken ct)
    {
        MergeDecisions applied = agent is null ? decisions : AgentDecisions.Combine(decisions, agent);
        IEnumerable<string> agentWarnings = agent is null ? [] : UnmatchedDecisions.Find(agent, result).Select(warning => Path.GetFileName(arguments.AgentDecisions!) + ": " + warning);
        foreach (string warning in result.ReadWarnings.Concat(result.WriteWarnings).Concat(UnmatchedDecisions.Find(decisions, result)).Concat(agentWarnings))
        {
            error.WriteLine("Warning: " + PrintableText.OneLine(warning));
        }

        AgentAudit? audit = agent is null ? null : new AgentAudit(decisions, agent, result);
        PendingDecisions pending = result.OutputWritten ? PendingDecisions.None : PendingDecisions.Find(result, applied);
        if (!pending.IsEmpty && arguments.Decisions is { } decisionsPath)
        {
            ct.ThrowIfCancellationRequested();
            DecisionsFile.Write(decisionsPath, decisions.WithPending(pending.Fields, pending.Findings));
        }

        WriteSummary(output, result, pending, audit);
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

        if (arguments.Brief is { } briefPath && arguments.AgentDecisions is null)
        {
            output.WriteLine("Brief: " + PrintableText.OneLine(briefPath));
        }

        if (arguments.Report is { } reportPath)
        {
            try
            {
                SecureFile.WriteAllText(reportPath, Report(result with { Output = RequestedOutput(arguments) }, pending, audit), overwrite: true, ct);
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

    private static void WriteSummary(TextWriter output, PipelineResult result, PendingDecisions pending, AgentAudit? audit)
    {
        CommandText.Count(output, "Items in the merged vault", result.Merged.Items.Count);
        CommandText.Count(output, "Merged groups", result.MergeReport.MergedItems);
        CommandText.Count(output, "Conflicts resolved by the policy", result.MergeReport.AutoResolvedConflicts);
        CommandText.Count(output, "Fields that need a decision", pending.Fields.Count);
        CommandText.Count(output, "Secret findings", result.Findings.Sum(item => item.Findings.Count));
        CommandText.Count(output, "  Applied", result.Findings.Sum(item => item.AcceptedFindingIds.Count));
        CommandText.Count(output, "  Need a decision", pending.FindingCount);
        if (audit is not null)
        {
            CommandText.Count(output, "  Rejected by agent", audit.Rejected.Sum(item => item.Choices.Count));
        }

        CommandText.Count(output, "Password issues", result.PasswordHealth.Issues.Count);
        CommandText.Count(output, "Attachments to upload after the import", result.AttachmentUploadCount);
        if (audit is null)
        {
            return;
        }

        CommandText.Count(output, "Agent decisions applied", audit.AppliedCount);
        CommandText.Count(output, "Agent decisions the decisions file overrides", audit.OverriddenCount);
        foreach (ItemFindingDecisions item in audit.Rejected)
        {
            foreach (FindingChoice choice in item.Choices)
            {
                output.WriteLine(PrintableText.OneLine($"Rejected by agent: {choice.FindingId} of {item.OriginKey} ({item.ItemName})"));
            }
        }
    }

    private static string PendingMessage(PendingDecisions pending, string? decisionsPath)
    {
        string counts = string.Create(CultureInfo.InvariantCulture, $"{pending.Fields.Count} field(s) and {pending.FindingCount} finding(s) need a decision.");
        return decisionsPath is null
            ? counts + " Run the merge again with --decisions <file> to write a pending entry for each to that file."
            : counts + " Pending entries are in " + PrintableText.OneLine(decisionsPath) + "; replace each with a decision and run the merge again.";
    }

    private static string Report(PipelineResult result, PendingDecisions pending, AgentAudit? audit)
    {
        var builder = new StringBuilder(VaultReport.ToMarkdown(result));
        if (audit is not null)
        {
            AppendDecidedByAgent(builder, audit.Applied);
        }

        IReadOnlyList<ItemFindingDecisions> rejectedByAgent = audit?.Rejected ?? [];
        if (rejectedByAgent.Count > 0)
        {
            builder.Append("\n## Rejected by agent\n\n")
                .Append("The agent decisions file rejects these pre-accepted findings, so each stays where it is.\n\n")
                .Append("| Item | Origin key | Finding |\n| --- | --- | --- |\n");
            foreach (ItemFindingDecisions item in rejectedByAgent)
            {
                foreach (FindingChoice choice in item.Choices)
                {
                    builder.Append("| ").Append(Cell(item.ItemName ?? string.Empty))
                        .Append(" | `").Append(Cell(item.OriginKey))
                        .Append("` | `").Append(Cell(choice.FindingId)).Append("` |\n");
                }
            }
        }

        if (pending.IsEmpty)
        {
            return builder.ToString();
        }

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

    // Every agent decision that took effect, so the user can check them before the import.
    private static void AppendDecidedByAgent(StringBuilder builder, MergeDecisions applied)
    {
        builder.Append("\n## Decided by agent\n\nThese decisions come from the agent decisions file; the decisions file overrides any of them it also decides.\n");
        if (applied.Groups.Count > 0)
        {
            builder.Append("\n| Group | Field | Value of |\n| --- | --- | --- |\n");
            foreach (GroupDecision group in applied.Groups)
            {
                foreach (FieldChoice choice in group.Choices)
                {
                    builder.Append("| `").Append(Cell(group.GroupKey)).Append("` | `").Append(Cell(choice.FieldPath))
                        .Append("` | `").Append(Cell(choice.OriginKey ?? string.Empty)).Append("` |\n");
                }
            }
        }

        if (applied.Findings.Count > 0)
        {
            builder.Append("\n| Origin key | Finding | Decision |\n| --- | --- | --- |\n");
            foreach (ItemFindingDecisions item in applied.Findings)
            {
                foreach (FindingChoice choice in item.Choices)
                {
                    builder.Append("| `").Append(Cell(item.OriginKey)).Append("` | `").Append(Cell(choice.FindingId))
                        .Append("` | ").Append(choice.Decision == FindingDecision.Accepted ? "accepted" : "rejected").Append(" |\n");
                }
            }
        }

        if (applied.Groups.Count == 0 && applied.Findings.Count == 0)
        {
            builder.Append("\nNo agent decision took effect.\n");
        }
    }

    private static string Cell(string text) => PrintableText.TableCell(text);

    // What the agent decided: the decisions that took effect, the pre-accepted findings it rejected, and how many of its
    // decisions the decisions file overrides.
    private sealed class AgentAudit(MergeDecisions decisions, MergeDecisions agent, PipelineResult result)
    {
        public MergeDecisions Applied { get; } = AgentDecisions.Effective(decisions, agent);

        public IReadOnlyList<ItemFindingDecisions> Rejected { get; } = AgentDecisions.RejectedByAgent(decisions, agent, result.Findings);

        public int AppliedCount => Applied.Groups.Sum(group => group.Choices.Count) + Applied.Findings.Sum(item => item.Choices.Count);

        public int OverriddenCount =>
            agent.Groups.Sum(group => group.Choices.Count(choice => choice.Kind != FieldChoiceKind.Pending))
            + agent.Findings.Sum(item => item.Choices.Count(choice => choice.Decision != FindingDecision.Pending))
            - AppliedCount;
    }
}
