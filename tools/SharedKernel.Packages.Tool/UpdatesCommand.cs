using System.CommandLine;
using System.Globalization;
using PandaBear.Tooling;

namespace PandaBear.Packages.Tool;

/// <summary>
/// Implements <c>updates</c>: for each declared cargo and npm package that is installed, finds the newest stable version
/// and judges it against advisories and the minimum release age. It contacts the network only with <c>--allow-network</c>
/// and changes nothing.
/// </summary>
internal static class UpdatesCommand
{
    /// <summary>The message printed when the network was not allowed.</summary>
    public const string RefusalMessage =
        "updates asks osv.dev, crates.io and registry.npmjs.org about the declared packages. Pass --allow-network to allow it. Nothing was contacted.";

    /// <summary>Builds the <c>updates</c> command.</summary>
    public static Command Create(string workingDirectory, Func<string, string?> resolveExecutable, IProcessRunner runner, Func<UpdateSources> createSources)
    {
        var rootOption = new Option<string>("--root")
        {
            Description = "Repository root directory. Defaults to the current directory.",
            HelpName = "path",
        };
        var ageOption = new Option<int?>("--minimum-release-age-days")
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
                resolveExecutable,
                runner,
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
        Func<string, string?> resolveExecutable,
        IProcessRunner runner,
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

        var (declared, configuredDays) = await PackagesConfigurationFile.ReadWithPolicy(root, cancellationToken).ConfigureAwait(false);
        var days = minimumAgeDays ?? configuredDays;
        if (days is null)
        {
            await error.WriteLineAsync("Give --minimum-release-age-days or set minimumReleaseAgeDays in .config/sharedkernel-packages.json.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return PlanCommand.FailureExitCode;
        }

        var age = MinimumReleaseAge.FromDays(days.Value, "--minimum-release-age-days");
        if (age.IsFailure)
        {
            await error.WriteLineAsync(ControlCharacters.Escape(age.ErrorDetails.Detail).AsMemory(), cancellationToken).ConfigureAwait(false);
            return PlanCommand.FailureExitCode;
        }

        var incomplete = false;
        List<PackageManagerKind> supported = [];
        foreach (var manager in declared.Managers)
        {
            if (manager is PackageManagerKind.Cargo or PackageManagerKind.Npm)
            {
                supported.Add(manager);
            }
            else
            {
                incomplete = true;
                await output.WriteLineAsync($"{PackageManagers.Id(manager)}: not supported by updates, which has release dates for cargo and npm only; skipped.".AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        var readers = new List<(PackageManagerKind Manager, IPackageReader Reader)>();
        var missing = false;
        foreach (var manager in supported)
        {
            var name = PackageManagers.Id(manager);
            var path = resolveExecutable(name);
            if (path is null)
            {
                await error.WriteLineAsync($"{name} was not found on PATH; {name} packages are declared.".AsMemory(), cancellationToken).ConfigureAwait(false);
                missing = true;
                continue;
            }

            readers.Add((manager, manager == PackageManagerKind.Cargo ? new CargoPackageReader(runner, path) : new NpmPackageReader(runner, path)));
        }

        if (missing)
        {
            return PlanCommand.FailureExitCode;
        }

        if (readers.Count == 0)
        {
            await output.WriteLineAsync("No cargo or npm packages are declared; nothing to check.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return incomplete ? PlanCommand.FailureExitCode : 0;
        }

        using var sources = createSources();
        var planner = new UpdatePlanner(sources.Advisories, sources.ReleaseDates, sources.Latest, sources.Time);
        var failed = false;
        var checkedCount = 0;
        var updateCount = 0;
        foreach (var (manager, reader) in readers)
        {
            var id = PackageManagers.Id(manager);
            var installed = await reader.ListInstalled(cancellationToken).ConfigureAwait(false);
            if (installed.IsFailure)
            {
                await Fail(error, id, installed.ErrorDetails.Detail, cancellationToken).ConfigureAwait(false);
                failed = true;
                continue;
            }

            var requirements = declared.For(manager).ToDictionary(static r => r.Name);
            List<InstalledPackage> toCheck = [];
            foreach (var package in installed.Value.Where(p => requirements.ContainsKey(p.Name)))
            {
                if (requirements[package.Name].Version is { } pinned)
                {
                    await output.WriteLineAsync($"{id}: {package.Name} is pinned to {pinned}; not checked.".AsMemory(), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    toCheck.Add(package);
                }
            }

            var found = await planner.FindUpdates(manager, toCheck, NetworkAccess.Allowed, cancellationToken).ConfigureAwait(false);
            if (found.IsFailure)
            {
                await Fail(error, id, found.ErrorDetails.Detail, cancellationToken).ConfigureAwait(false);
                failed = true;
                continue;
            }

            foreach (var unknown in found.Value.NotOnRegistry)
            {
                incomplete = true;
                await output.WriteLineAsync($"{id}: {unknown} is not on the registry; not checked.".AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            var decisions = await planner.Plan(manager, found.Value.Updates, age.Value, NetworkAccess.Allowed, cancellationToken).ConfigureAwait(false);
            if (decisions.IsFailure)
            {
                await Fail(error, id, decisions.ErrorDetails.Detail, cancellationToken).ConfigureAwait(false);
                failed = true;
                continue;
            }

            checkedCount += toCheck.Count - found.Value.NotOnRegistry.Count;
            updateCount += decisions.Value.Count;
            foreach (var decision in decisions.Value)
            {
                var candidate = decision.Candidate;
                await output.WriteLineAsync($"{id}: {decision.Kind} {candidate.Package} {candidate.InstalledVersion} -> {candidate.CandidateVersion}: {decision.Reason}".AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (failed)
        {
            return PlanCommand.FailureExitCode;
        }

        await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{updateCount} of {checkedCount} checked packages have a newer stable version.").AsMemory(), cancellationToken).ConfigureAwait(false);
        if (incomplete)
        {
            await error.WriteLineAsync("Some packages were not checked (see above), so this result is incomplete.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return incomplete ? PlanCommand.FailureExitCode : 0;
    }

    private static Task Fail(TextWriter error, string id, string detail, CancellationToken cancellationToken) =>
        error.WriteLineAsync($"{id}: {ControlCharacters.Escape(detail)}".AsMemory(), cancellationToken);
}
