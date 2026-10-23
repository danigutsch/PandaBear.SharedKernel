using System.CommandLine;
using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// <c>apply</c>: applies an edited catalog to the vault file it was made from and writes the result as a Bitwarden
/// export, password-protected unless <c>--plaintext</c> is given. It writes nothing while the catalog changes something
/// it may not.
/// </summary>
internal static class ApplyCommand
{
    private const string OutputFile = "output file";
    private const string CatalogFileName = "catalog file";

    /// <summary>Creates the command.</summary>
    /// <param name="environment">Password prompts, terminal state, and clock.</param>
    /// <returns>The command.</returns>
    public static Command Create(VaultCommandEnvironment environment)
    {
        Argument<string> input = CommandSymbols.Input();
        Option<string> catalog = CommandSymbols.FilePath(new Option<string>("--catalog")
        {
            Description = "Edited catalog, made from the input by the catalog command.",
            HelpName = "file",
            Required = true,
        });
        Option<string> output = CommandSymbols.FilePath(new Option<string>("--output", "-o")
        {
            Description = "File to write the edited vault to. It must not be the input or the catalog; an existing file is replaced only with --force.",
            HelpName = "file",
            Required = true,
        });
        Option<string> decisions = CommandSymbols.FilePath(new Option<string>("--decisions")
        {
            Description = "Decisions file with the notes decisions the catalog was made with.",
            HelpName = "file",
        });
        var plaintext = new Option<bool>("--plaintext")
        {
            Description = "Write unencrypted Bitwarden JSON instead of a password-protected export. Delete the file after the import.",
        };
        var force = new Option<bool>("--force") { Description = "Replace an existing output file." };
        var command = new Command("apply", "Apply an edited catalog to the vault it was made from and write a Bitwarden export, protected by a password unless --plaintext is given.")
        {
            input,
            catalog,
            output,
            decisions,
            plaintext,
            force,
        };
        CommandAction.Set(command, environment, (parseResult, prompts, ct) => Run(
            new ApplyArguments(
                parseResult.GetRequiredValue(input),
                parseResult.GetRequiredValue(catalog),
                parseResult.GetRequiredValue(output),
                parseResult.GetValue(decisions),
                parseResult.GetValue(plaintext),
                parseResult.GetValue(force)),
            parseResult.InvocationConfiguration,
            prompts,
            ct));
        return command;
    }

    private static int Run(ApplyArguments arguments, InvocationConfiguration invocation, PasswordPrompts prompts, CancellationToken ct)
    {
        TextWriter output = invocation.Output;
        TextWriter error = invocation.Error;
        string[] inputs = [arguments.Input];
        (string Path, string Name)[] others = [(arguments.Catalog, CatalogFileName)];
        if (!OutputFiles.CanWrite(arguments.Output, OutputFile, inputs, others, arguments.Force, error))
        {
            return ExitCodes.InputOutputProblem;
        }

        VaultFileFormat?[] formats = InputChecks.Detect(inputs, error);
        if (formats[0] is null || !PasswordsCanBeAsked(arguments, formats, prompts.IsInteractive, error))
        {
            return ExitCodes.InputOutputProblem;
        }

        CatalogDocument catalog = CatalogFile.Read(arguments.Catalog);
        string inputName = Path.GetFileName(arguments.Input);
        if (!string.Equals(catalog.Source.File, inputName, StringComparison.Ordinal))
        {
            error.WriteLine(PrintableText.OneLine($"The catalog was made from {catalog.Source.File}, not from {inputName}. Apply it to that file, or run catalog on {inputName}."));
            return ExitCodes.InputOutputProblem;
        }

        if (!string.Equals(catalog.Source.Sha256, CatalogFile.Hash(arguments.Input), StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine(PrintableText.OneLine($"{inputName} changed after the catalog was made. Run catalog on it again and redo the edits in the new catalog."));
            return ExitCodes.InputOutputProblem;
        }

        MergeDecisions decisions = arguments.Decisions is { } decisionsPath ? DecisionsFile.Read(decisionsPath) : MergeDecisions.None;
        var pipeline = new VaultPipeline(prompts.Environment);
        LoadedVault vault = pipeline.Load(inputs, ct);
        foreach (string warning in vault.Warnings)
        {
            error.WriteLine("Warning: " + PrintableText.OneLine(warning));
        }

        CatalogApplyResult applied = CatalogApplier.Apply(vault, catalog, decisions);
        if (applied.Vault is not { } edited)
        {
            foreach (string problem in applied.Problems)
            {
                error.WriteLine("Error: " + PrintableText.OneLine(problem));
            }

            error.WriteLine("The vault was not written because the catalog changes what it may not.");
            return ExitCodes.InputOutputProblem;
        }

        ct.ThrowIfCancellationRequested();
        BitwardenWriteResult written = pipeline.Write(edited, new PipelineOutput(arguments.Output, arguments.Plaintext, arguments.Force), [arguments.Input, arguments.Catalog], ct);
        foreach (string warning in written.Warnings)
        {
            error.WriteLine("Warning: " + PrintableText.OneLine(warning));
        }

        if (written.Errors.Count > 0)
        {
            foreach (string writeError in written.Errors)
            {
                error.WriteLine("Error: " + PrintableText.OneLine(writeError));
            }

            error.WriteLine("The vault was not written because Bitwarden would reject the import.");
            return ExitCodes.InputOutputProblem;
        }

        // Named only once the vault is written, because they are uploaded after its import.
        foreach (VaultItem item in edited.Items)
        {
            foreach (AttachmentRef attachment in item.Attachments)
            {
                error.WriteLine(PrintableText.OneLine($"Warning: \"{item.Name}\": attachment \"{attachment.FileName}\" is not in the written vault, because Bitwarden JSON imports cannot carry files. Upload it by hand after the import."));
            }
        }

        CommandText.Count(output, "Items", edited.Items.Count);
        CommandText.Count(output, "Items changed", applied.ChangedItems);
        string outputPath = PrintableText.OneLine(arguments.Output);
        output.WriteLine(arguments.Plaintext
            ? $"Wrote the vault to {outputPath} as unencrypted JSON. Delete it after the import."
            : $"Wrote the vault to {outputPath} as a password-protected export.");
        return ExitCodes.Success;
    }

    private static bool PasswordsCanBeAsked(ApplyArguments arguments, IReadOnlyList<VaultFileFormat?> formats, bool isInteractive, TextWriter error)
    {
        bool canAsk = InputChecks.PasswordsCanBeAsked([arguments.Input], formats, isInteractive, error);
        if (isInteractive || arguments.Plaintext)
        {
            return canAsk;
        }

        error.WriteLine("The vault is written as a password-protected export, and standard input is not a terminal to type its password in. Run the command in a terminal, or pass --plaintext to write unencrypted JSON.");
        return false;
    }

    private sealed record ApplyArguments(string Input, string Catalog, string Output, string? Decisions, bool Plaintext, bool Force);
}
