using System.CommandLine;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// <c>inspect</c>: shows, per input, the detected format, item counts by type, folders, items with and without
/// modification times, attachments in the export or missing from it, and read warnings. It shows no vault values.
/// </summary>
internal static class InspectCommand
{
    /// <summary>Creates the command.</summary>
    /// <param name="environment">Password prompts, terminal state, and clock.</param>
    /// <returns>The command.</returns>
    public static Command Create(VaultCommandEnvironment environment)
    {
        Argument<string[]> inputs = CommandSymbols.Inputs();
        var command = new Command("inspect", "Show each input's format, item counts by type, folders, modification times, attachments, and read warnings, without vault values.")
        {
            inputs,
        };
        CommandAction.Set(command, environment, (parseResult, prompts, ct) => Run(parseResult.GetRequiredValue(inputs), parseResult.InvocationConfiguration, prompts, ct));
        return command;
    }

    private static int Run(string[] inputs, InvocationConfiguration invocation, PasswordPrompts prompts, CancellationToken ct)
    {
        TextWriter output = invocation.Output;
        TextWriter error = invocation.Error;
        VaultFileFormat?[] formats = InputChecks.Detect(inputs, error);
        if (!InputChecks.PasswordsCanBeAsked(inputs, formats, prompts.IsInteractive, error))
        {
            return ExitCodes.InputOutputProblem;
        }

        bool failed = Array.IndexOf(formats, null) >= 0;
        bool first = true;
        for (int index = 0; index < inputs.Length; index++)
        {
            if (formats[index] is not { } format)
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();
            string name = Path.GetFileName(inputs[index]);
            VaultReadResult read;
            try
            {
                read = VaultFileReader.Read(inputs[index], fileName => prompts.Environment.ReadPassword("Password for " + fileName));
            }
            catch (Exception exception) when (CommandAction.IsFileProblem(exception))
            {
                error.WriteLine(CommandText.OneLine($"{name}: {exception.Message}"));
                failed = true;
                continue;
            }

            if (!first)
            {
                output.WriteLine();
            }

            first = false;
            Write(output, name, format, read);
        }

        return failed ? ExitCodes.InputOutputProblem : ExitCodes.Success;
    }

    private static void Write(TextWriter output, string name, VaultFileFormat format, VaultReadResult read)
    {
        IReadOnlyList<VaultItem> items = read.Export.Items;
        output.WriteLine(CommandText.OneLine(name));
        output.WriteLine("  Format: " + Describe(format));
        CommandText.Count(output, "  Items", items.Count);
        foreach (IGrouping<VaultItemType, VaultItem> type in items.GroupBy(item => item.Type).OrderBy(type => type.Key))
        {
            CommandText.Count(output, "    " + Describe(type.Key), type.Count());
        }

        int dated = items.Count(item => item.LastModified.HasValue);
        CommandText.Count(output, "  Items with a modification time", dated);
        CommandText.Count(output, "  Items without a modification time", items.Count - dated);

        string[] folders = [.. items.Select(item => item.FolderPath).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        CommandText.Count(output, "  Folders", folders.Length);
        foreach (string folder in folders)
        {
            output.WriteLine("    " + CommandText.OneLine(folder));
        }

        AttachmentRef[] attachments = [.. items.SelectMany(item => item.Attachments)];
        int included = attachments.Count(attachment => attachment.SourcePath is not null);
        CommandText.Count(output, "  Attachments in the export", included);
        CommandText.Count(output, "  Attachments missing from the export", attachments.Length - included);

        CommandText.Count(output, "  Warnings", read.Warnings.Count);
        foreach (string warning in read.Warnings)
        {
            output.WriteLine("    " + CommandText.OneLine(warning));
        }
    }

    private static string Describe(VaultFileFormat format) => format switch
    {
        VaultFileFormat.KeeperJson => "Keeper JSON",
        VaultFileFormat.KeeperJsonZip => "Keeper Commander zip",
        VaultFileFormat.KeeperCsv => "Keeper CSV",
        VaultFileFormat.BitwardenJson => "Bitwarden JSON",
        VaultFileFormat.BitwardenJsonPasswordProtected => "Bitwarden password-protected JSON",
        VaultFileFormat.BitwardenJsonAccountRestricted => "Bitwarden account-restricted JSON",
        VaultFileFormat.BitwardenCsv => "Bitwarden CSV",
        VaultFileFormat.BitwardenZip => "Bitwarden zip",
        _ => "unknown",
    };

    private static string Describe(VaultItemType type) => type switch
    {
        VaultItemType.Login => "Login",
        VaultItemType.SecureNote => "Secure note",
        VaultItemType.Card => "Card",
        VaultItemType.Identity => "Identity",
        VaultItemType.SshKey => "SSH key",
        _ => type.ToString(),
    };
}
