using System.CommandLine;
using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// <c>catalog</c>: writes the catalog of one vault file, which people and agents edit without seeing secret values, and
/// rebuilds the pending notes entries of the decisions file, one for each item whose notes have no decision.
/// </summary>
internal static class CatalogCommand
{
    private const string CatalogFileName = "catalog file";
    private const string DecisionsFileName = "decisions file";

    /// <summary>Creates the command.</summary>
    /// <param name="environment">Password prompts, terminal state, and clock.</param>
    /// <returns>The command.</returns>
    public static Command Create(VaultCommandEnvironment environment)
    {
        Argument<string> input = CommandSymbols.Input();
        Option<string> output = CommandSymbols.FilePath(new Option<string>("--output", "-o")
        {
            Description = "Catalog file to write. It must not be the input; an existing file is replaced only with --force.",
            HelpName = "file",
            Required = true,
        });
        Option<string> decisions = CommandSymbols.FilePath(new Option<string>("--decisions")
        {
            Description = "Decisions file with the notes decisions. Items whose notes have no decision get a pending entry in it.",
            HelpName = "file",
        });
        var force = new Option<bool>("--force") { Description = "Replace an existing catalog file." };
        var command = new Command("catalog", "Write the names, folders, website addresses, shared notes, and text fields of a vault to a JSON catalog that people and agents can edit, without secret values.")
        {
            input,
            output,
            decisions,
            force,
        };
        CommandAction.Set(command, environment, (parseResult, prompts, ct) => Run(
            parseResult.GetRequiredValue(input),
            parseResult.GetRequiredValue(output),
            parseResult.GetValue(decisions),
            parseResult.GetValue(force),
            parseResult.InvocationConfiguration,
            prompts,
            ct));
        return command;
    }

    private static int Run(string input, string catalogPath, string? decisionsPath, bool force, InvocationConfiguration invocation, PasswordPrompts prompts, CancellationToken ct)
    {
        TextWriter output = invocation.Output;
        TextWriter error = invocation.Error;
        string[] inputs = [input];
        bool canWrite = OutputFiles.CanWrite(catalogPath, CatalogFileName, inputs, [], force, error);
        if (decisionsPath is not null)
        {
            canWrite &= OutputFiles.CanWrite(decisionsPath, DecisionsFileName, inputs, [(catalogPath, CatalogFileName)], replaceExisting: true, error);
        }

        if (!canWrite)
        {
            return ExitCodes.InputOutputProblem;
        }

        VaultFileFormat?[] formats = InputChecks.Detect(inputs, error);
        if (formats[0] is null || !InputChecks.PasswordsCanBeAsked(inputs, formats, prompts.IsInteractive, error))
        {
            return ExitCodes.InputOutputProblem;
        }

        MergeDecisions decisions = decisionsPath is not null ? DecisionsFile.Read(decisionsPath) : MergeDecisions.None;
        string sha256 = CatalogFile.Hash(input);
        LoadedVault vault = new VaultPipeline(prompts.Environment).Load(inputs, ct);
        CatalogBuild built = CatalogBuilder.Build(vault, sha256, decisions);
        foreach (string warning in vault.Warnings)
        {
            error.WriteLine("Warning: " + PrintableText.OneLine(warning));
        }

        ct.ThrowIfCancellationRequested();
        CatalogFile.Write(catalogPath, built.Catalog, force);
        if (decisionsPath is not null && (built.PendingNotes.Count > 0 || decisions.Notes.Any(entry => entry.Decision == NotesDecision.Pending)))
        {
            DecisionsFile.Write(decisionsPath, decisions.WithPendingNotes(built.PendingNotes));
        }

        CatalogCounts counts = built.Counts;
        CommandText.Count(output, "Items", built.Catalog.Items.Count);
        CommandText.Count(output, "Notes shown", counts.SharedNotes);
        CommandText.Count(output, "Notes withheld until you decide", counts.PendingNotes);
        CommandText.Count(output, "Notes kept private", counts.PrivateNotes);
        CommandText.Count(output, "Notes that apply moves into a hidden field", counts.HiddenNotes);
        CommandText.Count(output, "Custom fields withheld", counts.ProtectedFields);
        CommandText.Count(output, "Website addresses masked", counts.MaskedUris);
        output.WriteLine($"Wrote the catalog to {PrintableText.OneLine(catalogPath)}. It holds names, folders, and addresses, and no passwords or other secret values.");
        if (counts.PendingNotes > 0)
        {
            output.WriteLine(decisionsPath is null
                ? "Pass --decisions <file> to list the withheld notes there, then set each entry to Share, Private, or Hidden and run catalog again."
                : $"Set each pending notes entry in {PrintableText.OneLine(decisionsPath)} to Share, Private, or Hidden, then run catalog again to show the shared notes.");
        }

        return ExitCodes.Success;
    }
}
