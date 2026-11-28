using System.CommandLine;
using SharedKernel.CommandLine;
using SharedKernel.InputNormalization;

namespace SharedKernel.Packages.Tool;

/// <summary>Opens package review and accepts confirmed declaration edits when both streams are terminal streams.</summary>
internal static class PackageReviewCommand
{
    /// <summary>Builds the package review command.</summary>
    internal static Command Create(string workingDirectory, PackageInventory inventory, Func<UpdateSources> createSources, PackageReviewTerminal terminal)
    {
        var rootOption = new RootOption(workingDirectory);
        var networkOption = new Option<bool>("--allow-network")
        {
            Description = "Allows update lookups at osv.dev, crates.io and registry.npmjs.org. The default reviews the declaration and installed state.",
        };
        var ageOption = UpdatesCommand.MinimumAgeOption();
        var command = new Command("review", "Review package declarations, installed differences, and optional update decisions. Confirm changes to the declaration in a terminal.")
        {
            rootOption,
            networkOption,
            ageOption,
        };
        command.SetAction((parseResult, cancellationToken) => PackagesCommandLine.Execute(
            parseResult,
            (output, error) => Run(
                rootOption.GetExistingDirectory(parseResult),
                parseResult.GetValue(networkOption),
                parseResult.GetValue(ageOption),
                inventory,
                createSources,
                terminal,
                output,
                error,
                cancellationToken),
            cancellationToken));
        return command;
    }

    private static async Task<int> Run(
        string root,
        bool allowNetwork,
        int? minimumAgeDays,
        PackageInventory inventory,
        Func<UpdateSources> createSources,
        PackageReviewTerminal terminal,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!terminal.IsInteractive())
        {
            return allowNetwork
                ? await UpdatesCommand.Run(root, minimumAgeDays, NetworkAccess.Allowed, inventory, createSources, output, error, cancellationToken).ConfigureAwait(false)
                : await PlanCommand.Run(root, inventory, output, error, cancellationToken).ConfigureAwait(false);
        }

        var edit = await PackageConfigurationEdit.Open(root, cancellationToken).ConfigureAwait(false);
        var configuration = edit.Configuration;
        MinimumReleaseAge? age = null;
        if (allowNetwork)
        {
            var minimumAge = UpdatesCommand.ResolveMinimumAge(configuration, minimumAgeDays);
            if (minimumAge.IsFailure)
            {
                await error.WriteLineAsync(ControlCharacters.Escape(minimumAge.ErrorDetails.Detail).AsMemory(), cancellationToken).ConfigureAwait(false);
                return PlanCommand.FailureExitCode;
            }

            age = minimumAge.Value;
        }

        var plan = await inventory.Plan(configuration.Packages, cancellationToken).ConfigureAwait(false);
        var updates = age is null
            ? null
            : await inventory.CheckUpdates(configuration.Packages, age, NetworkAccess.Allowed, createSources, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var session = new PackageReviewSession(configuration.Packages, plan, updates, edit);
        var exitCode = terminal.Run(session, cancellationToken);
        if (session.ConfirmedEdit is { } confirmed)
        {
            await edit.Save(confirmed, cancellationToken).ConfigureAwait(false);
        }

        return exitCode;
    }
}
