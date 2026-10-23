using System.CommandLine;
using System.Globalization;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Review;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// <c>review</c>: opens the full-screen review UI on the inputs. It refuses to start unless standard input and output are
/// terminals, checks the files and inputs as <c>merge</c> does, and asks for input passwords on the console before the UI
/// starts. The UI saves decisions and writes the merged vault; its password is asked inside the UI.
/// </summary>
internal static class ReviewCommand
{
    private const string OutputFile = "output file";
    private const string DecisionsFileName = "decisions file";

    /// <summary>Creates the command for the terminal of the current process.</summary>
    /// <param name="environment">Password prompts, terminal state, and clock.</param>
    /// <returns>The command.</returns>
    public static Command Create(VaultCommandEnvironment environment) => Create(environment, ReviewTerminal.Console);

    /// <summary>Creates the command for a terminal.</summary>
    /// <param name="environment">Password prompts, terminal state, and clock.</param>
    /// <param name="terminal">Terminal that runs the review UI.</param>
    /// <returns>The command.</returns>
    public static Command Create(VaultCommandEnvironment environment, ReviewTerminal terminal)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(terminal);

        Argument<string[]> inputs = CommandSymbols.Inputs();
        Option<string> decisions = CommandSymbols.FilePath(new Option<string>("--decisions")
        {
            Description = "Decisions file to start from and to save to, readable only by you. Without it, decisions cannot be saved.",
            HelpName = "file",
        });
        Option<string> output = CommandSymbols.FilePath(new Option<string>("--output", "-o")
        {
            Description = "File to write the merged vault to. It must not be an input; an existing file is replaced only with --force. Without it, the merged vault cannot be written.",
            HelpName = "file",
        });
        var plaintext = new Option<bool>("--plaintext")
        {
            Description = "Write unencrypted Bitwarden JSON instead of a password-protected export. Delete the file after the import.",
        };
        var force = new Option<bool>("--force") { Description = "Replace an existing output file." };
        var command = new Command("review", "Review groups, field differences, and findings in a full-screen terminal UI, save decisions, and write the merged vault.")
        {
            inputs,
            decisions,
            output,
            plaintext,
            force,
        };
        command.Validators.Add(result =>
        {
            if (result.GetValue(output) is null && (result.GetValue(plaintext) || result.GetValue(force)))
            {
                result.AddError("--plaintext and --force apply to the merged vault; give --output <file> or leave them out.");
            }
        });
        command.SetAction((parseResult, ct) => Run(
            new Arguments(
                parseResult.GetRequiredValue(inputs),
                parseResult.GetValue(decisions),
                parseResult.GetValue(output),
                parseResult.GetValue(plaintext),
                parseResult.GetValue(force)),
            parseResult.InvocationConfiguration,
            environment,
            terminal,
            ct));
        return command;
    }

    private static async Task<int> Run(Arguments arguments, InvocationConfiguration invocation, VaultCommandEnvironment environment, ReviewTerminal terminal, CancellationToken ct)
    {
        TextWriter error = invocation.Error;
        if (!environment.IsInteractive || !terminal.OutputIsTerminal())
        {
            await error.WriteLineAsync("The review UI needs a terminal, and standard input or output is not one. To decide without the UI, run merge with --decisions <file> and edit the pending entries of that file.").ConfigureAwait(false);
            return ExitCodes.InputOutputProblem;
        }

        var prompts = new PasswordPrompts(environment, ct);

        // The checks, the password prompts, and reading the inputs block on the terminal and the file system. They run on
        // the thread pool, so Ctrl+C ends the command at once, as it does for the other commands.
        (int exitCode, ReviewSession? session) = await Task.Run(
            () => CommandAction.Guard(error, prompts, () => Prepare(arguments, prompts, error, ct), static code => (code, null), ct),
            ct).WaitAsync(ct).ConfigureAwait(false);
        if (session is null)
        {
            return exitCode;
        }

        // The UI restores the terminal before it returns, also when the token is canceled, so the command waits for it
        // instead of ending with the terminal still in the UI's mode.
        exitCode = await Task.Run(() => CommandAction.Guard(error, prompts, () => terminal.Run(session, ct), static code => code, ct), CancellationToken.None).ConfigureAwait(false);
        Summarize(invocation.Output, error, session);
        return exitCode;
    }

    private static (int ExitCode, ReviewSession? Session) Prepare(Arguments arguments, PasswordPrompts prompts, TextWriter error, CancellationToken ct)
    {
        if (!CanWriteFiles(arguments, error))
        {
            return (ExitCodes.InputOutputProblem, null);
        }

        VaultFileFormat?[] formats = InputChecks.Detect(arguments.Inputs, error);
        if (Array.IndexOf(formats, null) >= 0 || !InputChecks.PasswordsCanBeAsked(arguments.Inputs, formats, prompts.IsInteractive, error))
        {
            return (ExitCodes.InputOutputProblem, null);
        }

        MergeDecisions decisions = arguments.Decisions is { } decisionsPath ? DecisionsFile.Read(decisionsPath) : MergeDecisions.None;
        ScannedVault scanned = VaultPipeline.Scan(new VaultPipeline(prompts.Environment).Load(arguments.Inputs, ct), ct);
        PipelineOutput? output = arguments.Output is { } outputPath ? new PipelineOutput(outputPath, arguments.Plaintext, arguments.Force) : null;
        var session = new ReviewSession(prompts.Environment, scanned, decisions, new ReviewFiles(arguments.Decisions, output));

        // Shown before the UI takes over the screen, so they are on the terminal again after the review ends.
        foreach (string warning in session.Result.ReadWarnings)
        {
            error.WriteLine("Warning: " + PrintableText.OneLine(warning));
        }

        foreach (string warning in UnmatchedDecisions.Find(session.Decisions, session.Result))
        {
            error.WriteLine("Warning: " + PrintableText.OneLine(warning));
        }

        return (ExitCodes.Success, session);
    }

    private static bool CanWriteFiles(Arguments arguments, TextWriter error)
    {
        bool canWrite = true;
        (string Path, string Name)[] others = [];
        if (arguments.Output is { } output)
        {
            canWrite &= OutputFiles.CanWrite(output, OutputFile, arguments.Inputs, [], arguments.Force, error);
            others = [(output, OutputFile)];
        }

        if (arguments.Decisions is { } decisions)
        {
            canWrite &= OutputFiles.CanWrite(decisions, DecisionsFileName, arguments.Inputs, others, replaceExisting: true, error);
        }

        return canWrite;
    }

    // The review writes no report, so the summary also names what the report of merge lists: the attachments to upload
    // after the import, the writer's warnings, and the accepted findings that placement could not move. They come from
    // the merge that wrote the vault, because that file is the one to import, or from the latest merge.
    private static void Summarize(TextWriter output, TextWriter error, ReviewSession session)
    {
        PipelineResult result = session.WriteResult ?? session.Result;
        foreach (string warning in result.WriteWarnings)
        {
            error.WriteLine("Warning: " + PrintableText.OneLine(warning));
        }

        foreach (PlacementProblem problem in result.PlacementProblems)
        {
            error.WriteLine("Warning: \"" + PrintableText.OneLine(problem.ItemName) + "\": " + PrintableText.OneLine(problem.Problem));
        }

        if (session.Saved && session.Files.DecisionsPath is { } saved)
        {
            output.WriteLine("Saved the decisions to " + PrintableText.OneLine(saved) + ".");
        }

        if (session.Written && session.Files.Output is { } written)
        {
            string writtenPath = PrintableText.OneLine(written.Path);
            output.WriteLine(written.Plaintext
                ? "Wrote the merged vault to " + writtenPath + " as unencrypted JSON. Delete it after the import."
                : "Wrote the merged vault to " + writtenPath + " as a password-protected export.");
        }

        CommandText.Count(output, "Fields that need a decision", session.Pending.Fields.Count);
        CommandText.Count(output, "Findings that need a decision", session.Pending.FindingCount);
        CommandText.Count(output, "Attachments to upload after the import", result.AttachmentUploadCount);
        if (session.Written)
        {
            foreach (ManualAttachment attachment in result.ManualAttachments)
            {
                output.WriteLine("  " + Describe(attachment));
            }

            foreach (UnassignedAttachmentFolder folder in result.UnassignedAttachmentFolders)
            {
                foreach (AttachmentRef file in folder.Files)
                {
                    output.WriteLine("  " + Describe(folder, file));
                }
            }
        }

        if (session.IsDirty)
        {
            error.WriteLine("Decisions changed since the last save were not saved.");
        }
    }

    // Names the file and where to find it, as the attachments table of the merge report does.
    private static string Describe(ManualAttachment attachment)
    {
        string file = PrintableText.OneLine(attachment.ItemName) + ": " + PrintableText.OneLine(attachment.FileName);
        return attachment.ArchivePath is { } archivePath
            ? file + ", in " + PrintableText.OneLine(attachment.SourceFile) + " at " + PrintableText.OneLine(archivePath)
            : file + ", not included in " + PrintableText.OneLine(attachment.SourceFile) + "; download it from the source vault";
    }

    // Names a file of a Bitwarden zip whose item is unknown and where to find it, as the table of unassigned folders in the
    // merge report does.
    private static string Describe(UnassignedAttachmentFolder folder, AttachmentRef file)
    {
        string location = PrintableText.OneLine(file.FileName) + ", in " + PrintableText.OneLine(folder.SourceFile) + " at " + PrintableText.OneLine(file.SourcePath ?? file.FileName);
        return folder.MatchingItemCount == 0
            ? location + "; its folder matches no item, so upload it to the item it belongs to"
            : location + string.Create(CultureInfo.InvariantCulture, $"; its folder matches {folder.MatchingItemCount} items, so upload it to the one it belongs to");
    }

    private sealed record Arguments(IReadOnlyList<string> Inputs, string? Decisions, string? Output, bool Plaintext, bool Force);
}
