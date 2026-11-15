using System.CommandLine;
using System.Globalization;
using PandaBear.InputNormalization;

namespace PandaBear.Packages.Tool;

/// <summary>
/// Implements <c>updates</c>: has <see cref="PackageInventory"/> judge the newest stable version of each declared cargo
/// and npm package that is installed against advisories and the minimum release age, and prints the decisions. It
/// contacts the network only with <c>--allow-network</c> and changes nothing.
/// </summary>
internal static class UpdatesCommand
{
    /// <summary>The message printed when the network was not allowed.</summary>
    public const string RefusalMessage =
        "updates asks osv.dev, crates.io and registry.npmjs.org about the declared packages. Pass --allow-network to allow it. Nothing was contacted.";

    private const string AgeOptionName = "--minimum-release-age-days";

    /// <summary>Builds the <c>updates</c> command.</summary>
    public static Command Create(string workingDirectory, PackageInventory inventory, Func<UpdateSources> createSources)
    {
        var rootOption = new Option<string>("--root")
        {
            Description = "Repository root directory. Defaults to the current directory.",
            HelpName = "path",
        };
        var ageOption = new Option<int?>(AgeOptionName)
        {
            Description = "Days a release must have been public before it is allowed, 0 to 3650. Defaults to minimumReleaseAgeDays in the configuration; one of the two is required.",
            HelpName = "days",
        };
        var networkOption = new Option<bool>("--allow-network")
        {
            Description = "Required. Allows requests to osv.dev, crates.io, and registry.npmjs.org. Without it nothing is contacted and the command refuses.",
        };
        var command = new Command("updates", "Judge the newest stable version of each declared cargo and npm package against advisories and the minimum release age. Changes nothing.")
        {
            rootOption,
            ageOption,
            networkOption,
        };
        command.SetAction((parseResult, cancellationToken) => PackagesCommandLine.Execute(
            parseResult,
            (output, error) => Run(
                Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory),
                parseResult.GetValue(ageOption),
                parseResult.GetValue(networkOption) ? NetworkAccess.Allowed : NetworkAccess.Denied,
                inventory,
                createSources,
                output,
                error,
                cancellationToken),
            cancellationToken));
        return command;
    }

    /// <summary>
    /// Returns 0 when every lookup succeeded and the decisions were printed, and <see cref="PlanCommand.FailureExitCode"/>
    /// when the network was not allowed, the configuration or a flag is invalid, a tool is missing or unreadable, any
    /// lookup failed, or anything could not be checked (an unsupported manager, a package that is not on its registry).
    /// No decision is printed for a manager whose lookups failed; the other packages are still judged and printed, so a
    /// green exit code always means a complete result.
    /// </summary>
    public static async Task<int> Run(
        string root,
        int? minimumAgeDays,
        NetworkAccess access,
        PackageInventory inventory,
        Func<UpdateSources> createSources,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (access != NetworkAccess.Allowed)
        {
            await error.WriteLineAsync(RefusalMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
            return PlanCommand.FailureExitCode;
        }

        var configuration = await PackagesConfiguration.Read(root, cancellationToken).ConfigureAwait(false);
        MinimumReleaseAge minimumAge;
        if (minimumAgeDays is { } days)
        {
            var age = MinimumReleaseAge.FromDays(days, AgeOptionName);
            if (age.IsFailure)
            {
                await error.WriteLineAsync(ControlCharacters.Escape(age.ErrorDetails.Detail).AsMemory(), cancellationToken).ConfigureAwait(false);
                return PlanCommand.FailureExitCode;
            }

            minimumAge = age.Value;
        }
        else if (configuration.MinimumReleaseAge is { } configured)
        {
            minimumAge = configured;
        }
        else
        {
            await error.WriteLineAsync($"Give {AgeOptionName} or set minimumReleaseAgeDays in {PackagesConfiguration.RelativePath}.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return PlanCommand.FailureExitCode;
        }

        var report = await inventory.CheckUpdates(configuration.Packages, minimumAge, access, createSources, cancellationToken).ConfigureAwait(false);
        foreach (var manager in report.Unsupported)
        {
            await output.WriteLineAsync($"{PackageManagers.Id(manager)}: not supported by updates, which has release dates for cargo and npm only; skipped.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (report.MissingTools.Count > 0)
        {
            await PackagesCommandLine.WriteMissing(error, report.MissingTools, cancellationToken).ConfigureAwait(false);
            return PlanCommand.FailureExitCode;
        }

        if (report.Managers.Count == 0)
        {
            await output.WriteLineAsync("No cargo or npm packages are declared; nothing to check.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return report.HasSkipped ? PlanCommand.FailureExitCode : 0;
        }

        foreach (var manager in report.Managers)
        {
            await Write(manager, output, error, cancellationToken).ConfigureAwait(false);
        }

        if (report.HasFailure)
        {
            return PlanCommand.FailureExitCode;
        }

        await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{report.UpdateCount} of {report.CheckedCount} checked packages have a newer stable version.").AsMemory(), cancellationToken).ConfigureAwait(false);
        if (report.HasSkipped)
        {
            await error.WriteLineAsync("Some packages were not checked (see above), so this result is incomplete.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return PlanCommand.FailureExitCode;
        }

        return 0;
    }

    private static async Task Write(ManagerUpdates manager, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var id = PackageManagers.Id(manager.Manager);
        foreach (var pinned in manager.Pinned)
        {
            await output.WriteLineAsync($"{id}: {pinned.Name} is pinned to {pinned.Version}; not checked.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var unknown in manager.NotOnRegistry)
        {
            await output.WriteLineAsync($"{id}: {unknown} is not on the registry; not checked.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (manager.Error is not null)
        {
            await PackagesCommandLine.WriteError(error, manager.Manager, manager.Error, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var decision in manager.Decisions)
        {
            var candidate = decision.Candidate;
            await output.WriteLineAsync($"{id}: {decision.Kind} {candidate.Package} {candidate.InstalledVersion} -> {candidate.CandidateVersion}: {decision.Reason}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }
}
