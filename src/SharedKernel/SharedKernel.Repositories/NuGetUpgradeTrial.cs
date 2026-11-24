using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using PandaBear.Processes;

namespace PandaBear.Repositories;

/// <summary>Runs a repository-selected NuGet upgrade in an isolated checkout and prepares review evidence.</summary>
public static class NuGetUpgradeTrial
{
    private const string DiagnosticRestoreOption = "-p:WarningsNotAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904";
    private const string CandidateFileAppLockedRestore = "candidateFileAppLockedRestore";

    /// <summary>
    /// Produces a JSON report and, when the trial is verified, a props and lockfile patch. The selected repository's
    /// SDK, package mappings, audit properties, and suppressions remain in effect during restore.
    /// </summary>
    /// <param name="options">The trial input and output paths.</param>
    /// <param name="output">Human-readable results.</param>
    /// <param name="error">Diagnostic messages for an incomplete trial.</param>
    /// <param name="cancellationToken">Cancels the trial and its child processes.</param>
    /// <returns>Zero for a verified patch or a verified no-op, or one when the trial cannot issue a patch.</returns>
    /// <exception cref="ArgumentNullException">The options or a writer is null.</exception>
    public static async Task<int> Run(
        NuGetUpgradeTrialOptions options, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            var root = Path.GetFullPath(options.Root);
            var patchPath = NuGetTrialWorkspace.RepositoryPath(root, options.PlanPath);
            var reportPath = NuGetTrialWorkspace.RepositoryPath(root, options.PlanPath + ".json");
            if (File.Exists(patchPath) || File.Exists(reportPath))
            {
                throw new ArgumentException("Choose new output paths for the patch and its report.");
            }

            ValidateSelection(options);
            var revision = await new RoadmapGitRepository().ReadHead(root, cancellationToken).ConfigureAwait(false);
            using var workspace = new NuGetTrialWorkspace();
            await workspace.Initialize(root, revision, options.LocalSources, cancellationToken).ConfigureAwait(false);
            var (report, patch) = await Evaluate(workspace, revision, options, cancellationToken).ConfigureAwait(false);
            var json = JsonSerializer.Serialize(report, NuGetRestoreGraphJsonContext.Default.NuGetUpgradeTrialReport);
            List<AtomicFileWrite> writes = [new(reportPath, json + "\n", null)];
            if (patch is not null)
            {
                writes.Add(new(patchPath, patch, null));
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
            RollbackFileWriteBatch.Apply(root, writes);
            var succeeded = report.Status is "verified" or "noChanges";
            await (succeeded ? output : error).WriteLineAsync(report.Reason.AsMemory(), cancellationToken).ConfigureAwait(false);
            return succeeded ? 0 : 1;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException
            or IOException or UnauthorizedAccessException or XmlException or JsonException or TimeoutException)
        {
            await error.WriteLineAsync("Could not prepare the isolated NuGet trial: ".AsMemory(), cancellationToken).ConfigureAwait(false);
            await error.WriteLineAsync(exception.Message.AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }
    }

    private static void ValidateSelection(NuGetUpgradeTrialOptions options)
    {
        if (!options.Vulnerable && options.RemovePin is null && options.Updates.Count == 0)
        {
            throw new ArgumentException("Select a central pin, an explicit package update, or the SDK vulnerable mode.");
        }

        if (options.Vulnerable && options.Updates.Count != 0)
        {
            throw new ArgumentException("Choose either explicit package updates or the SDK vulnerable mode.");
        }

        if (options.RemovePin is not null && !NuGetTrialProps.IsPackageId(options.RemovePin))
        {
            throw new ArgumentException("The pin must be a NuGet package identifier.");
        }

        if (options.ProposePinUpdates && (options.RemovePin is null || options.Vulnerable || options.Updates.Count != 0))
        {
            throw new ArgumentException("Use --propose with --remove-pin only, without an exact update or vulnerable mode.");
        }

        if (options.CoordinatedPackages.Any(package => !NuGetTrialProps.IsPackageId(package))
            || (options.CoordinatedPackages.Count != 0 && !options.ProposePinUpdates && !options.Vulnerable))
        {
            throw new ArgumentException("Use --coordinate with --propose or --vulnerable and specify valid direct package IDs.");
        }

        if (!options.PropsPath.EndsWith(".props", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Select the .props file owning the central declarations.");
        }
    }

    private static async Task<(NuGetUpgradeTrialReport Report, string? Patch)> Evaluate(
        NuGetTrialWorkspace workspace, string revision, NuGetUpgradeTrialOptions options, CancellationToken ct)
    {
        var root = workspace.Root;
        var updates = NuGetTrialProps.ParseUpdates(options.Updates);
        if (options.RemovePin is not null && updates.ContainsKey(options.RemovePin))
        {
            throw new ArgumentException("The same central declaration cannot be updated and removed.");
        }

        var solution = await NuGetRestoreGraphComparison.FindSolution(root, revision, ct).ConfigureAwait(false);
        var props = NuGetTrialWorkspace.RepositoryPath(root, options.PropsPath);
        var propsRelative = Path.GetRelativePath(root, props).Replace('\\', '/');
        List<NuGetTrialStep> steps = [];
        List<NuGetTrialPackageEvidence> evidence = [];
        NuGetRestoreGraphReport? graph = null;
        var initial = await Step(steps, root, "baselineLockedRestore", ["restore", solution, "--locked-mode", "--force", "--no-http-cache", "--disable-build-servers"], ct)
            .ConfigureAwait(false);
        var diagnosticBaseline = initial.ExitCode != 0 && initial.Codes.Count != 0 && initial.Codes.All(IsAdvisory);
        if (diagnosticBaseline)
        {
            initial = await Step(steps, root, "baselineAdvisoryDiagnostics",
                ["restore", solution, "--locked-mode", "--force", "--no-http-cache", "--disable-build-servers", DiagnosticRestoreOption], ct).ConfigureAwait(false);
        }

        if (initial.AuditUnavailable)
        {
            return Report(options, revision, steps, evidence, graph, "auditUnavailable", "The baseline audit source is unavailable.");
        }

        if (initial.ExitCode != 0)
        {
            return Report(options, revision, steps, evidence, graph, "blocked", "The baseline did not pass the configured locked restore and audit.");
        }

        var baseline = await NuGetRestoreGraphComparison.Read(root, null, ct,
            diagnosticBaseline ? [DiagnosticRestoreOption] : null).ConfigureAwait(false);
        if (baseline.Projects.Values.Any(project => project.Unavailable is not null))
        {
            return Report(options, revision, steps, evidence, graph, "blocked", "The baseline restore graph is incomplete.");
        }

        List<NuGetFileApp> fileApps = [];
        foreach (var file in await NuGetFileApps.Find(root, ct).ConfigureAwait(false))
        {
            fileApps.Add(await NuGetFileApps.Evaluate(root, file, ct).ConfigureAwait(false));
        }

        baseline = baseline with { FileApps = fileApps };
        if (options.RemovePin is { } pin)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var direct = baseline.Projects.SelectMany(project =>
                project.Value.Graph!.FindPackage(pin)
                    .Where(match => match.Package.Classification == NuGetRestoreClassification.Direct)
                    .Select(match => $"{project.Key} ({match.Target})"))
                .Concat(fileApps.Where(app => app.Packages.TryGetValue(pin, out var package)
                        && string.Equals(package.CentralFile, propsRelative, comparison))
                    .Select(app => $"{app.Path} ({app.Framework})")).ToArray();
            if (direct.Length != 0)
            {
                await RecordPackageEvidence(evidence, workspace, solution, baseline, pin, steps, ct).ConfigureAwait(false);
                return Report(options, revision, steps, evidence, graph, "pinRequired",
                    "Retain the direct declaration for " + pin + " in " + string.Join(", ", direct) + ".");
            }
        }

        var fileAppFailure = await RestoreFileApps(workspace, baseline, fileApps, steps, baselineRestore: true, ct).ConfigureAwait(false);
        await RecordPackageEvidence(evidence, workspace, solution, baseline, options.RemovePin, steps, ct).ConfigureAwait(false);
        if (fileAppFailure is not null)
        {
            return Report(options, revision, steps, evidence, graph, "blocked", fileAppFailure);
        }

        if (options.ProposePinUpdates && options.RemovePin is { } proposedPin
            && await NuGetTrialProps.HasDeclaration(props, proposedPin, ct).ConfigureAwait(false))
        {
            var proposal = await ProposePinOwners(steps, root, solution, props, proposedPin, baseline, options.CoordinatedPackages, ct).ConfigureAwait(false);
            if (proposal?.AuditUnavailable == true)
            {
                return Report(options, revision, steps, evidence, graph, "auditUnavailable", "The SDK could not read an audit source.");
            }

            if (proposal is { ExitCode: not 0 })
            {
                return Report(options, revision, steps, evidence, graph, "pinRequired",
                    "The SDK did not find a compatible update for the selected direct owners and coordinated packages.");
            }
        }

        if (options.Vulnerable && steps.Any(step => step.Codes.Any(IsAdvisory)))
        {
            return await EvaluateMinimum(workspace, options, baseline, props, propsRelative, steps, evidence, ct).ConfigureAwait(false);
        }

        if (options.RemovePin is not null || updates.Count != 0)
        {
            await NuGetTrialProps.Apply(props, options.RemovePin, updates, ct).ConfigureAwait(false);
        }

        return await EvaluateCandidate(workspace, options, baseline, propsRelative, [], steps, evidence, ct).ConfigureAwait(false);
    }

    private static async Task<(NuGetUpgradeTrialReport Report, string? Patch)> EvaluateMinimum(
        NuGetTrialWorkspace workspace, NuGetUpgradeTrialOptions options, NuGetRestoreGraphSource baseline,
        string props, string propsRelative, List<NuGetTrialStep> steps, List<NuGetTrialPackageEvidence> evidence, CancellationToken ct)
    {
        var advisories = steps.SelectMany(step => step.Advisories).Distinct().ToArray();
        var owners = NuGetAuditMinimum.Owners(baseline, advisories, options.CoordinatedPackages);
        if (advisories.Length == 0 || owners is null || owners.Length == 0)
        {
            return Incomplete("ownershipUnavailable", "The advisory could not be tied to an evaluated direct package owner.", 0, []);
        }

        if (owners.Length > NuGetAuditMinimum.OwnerLimit)
        {
            return Incomplete("limitReached", "The advisory owner set exceeds the search limit.", 0, []);
        }

        var requests = NuGetAuditMinimum.Requests(baseline, owners);
        var exact = await NuGetTrialProps.ExactDeclarations(props, owners, ct).ConfigureAwait(false);
        if (requests is null || exact is null)
        {
            return Incomplete("ownershipUnavailable",
                "The trial needs evaluated sources and a single literal declaration in the selected props for every owner.", 0, []);
        }

        var catalog = await NuGetVersionCatalog.Read(workspace.Root, baseline.SdkVersion, requests, ct).ConfigureAwait(false);
        if (catalog.Any(package => package.Status != "complete"))
        {
            return Incomplete("metadataUnavailable",
                "The SDK could not enumerate every eligible mapped source. The minimum is unproven.", 0, catalog);
        }

        (NuGetUpgradeTrialReport Report, string? Patch, string[] Updates)? verified = null;
        var search = await NuGetAuditMinimum.Search(
            NuGetAuditMinimum.Candidates(catalog, includeUnchanged: options.RemovePin is not null), TryCandidate, ct).ConfigureAwait(false);
        if (verified is { } selected)
        {
            return (selected.Report with { Minimum = new("proven", search.Attempts, selected.Updates, catalog) }, selected.Patch);
        }

        return search.Status switch
        {
            "limitReached" => Incomplete(search.Status, "The candidate limit was reached. The minimum is unproven.", search.Attempts, catalog),
            "candidateUnavailable" => Incomplete(search.Status,
                "An earlier candidate could not be evaluated conclusively. The minimum is unproven.", search.Attempts, catalog),
            _ => (Report(options, baseline.Revision, steps, evidence, null, "unresolvedAdvisories",
                "No candidate in the complete mapped owner search passed the repository policy.").Report with
            {
                Minimum = new("exhausted", search.Attempts, [], catalog),
            }, null),
        };

        async Task<NuGetAuditAttemptResult> TryCandidate(KeyValuePair<string, string>[] candidate, CancellationToken cancellation)
        {
            var updates = candidate.ToDictionary(pair => pair.Key, pair => exact.Contains(pair.Key) ? "[" + pair.Value + "]" : pair.Value,
                StringComparer.OrdinalIgnoreCase);
            try
            {
                await workspace.ResetCandidate(cancellation).ConfigureAwait(false);
                await NuGetTrialProps.Apply(props, options.RemovePin, updates, cancellation).ConfigureAwait(false);
                var result = await EvaluateCandidate(workspace, options, baseline, propsRelative, candidate, steps, evidence, cancellation)
                    .ConfigureAwait(false);
                if (result.Report.Status is "verified" or "noChanges")
                {
                    verified = (result.Report, result.Patch, UpdateLabels(candidate));
                    return NuGetAuditAttemptResult.Verified;
                }

                if (!IsConclusiveRejection(result.Report.Status, steps[^1]))
                {
                    return NuGetAuditAttemptResult.Unavailable;
                }

                // A rejected attempt must leave no untracked lock before a later one begins.
                await workspace.ReviewPatch(propsRelative, cancellation).ConfigureAwait(false);
                return NuGetAuditAttemptResult.Rejected;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or JsonException
                or InvalidOperationException or KeyNotFoundException or TimeoutException)
            {
                return NuGetAuditAttemptResult.Unavailable;
            }
        }

        (NuGetUpgradeTrialReport, string?) Incomplete(string status, string reason, int attempts, NuGetVersionSet[] catalog)
        {
            var report = Report(options, baseline.Revision, steps, evidence, null, "minimumUnproven", reason).Report;
            return (report with { Minimum = new(status, attempts, [], catalog) }, null);
        }
    }

    private static async Task<(NuGetUpgradeTrialReport Report, string? Patch)> EvaluateCandidate(
        NuGetTrialWorkspace workspace, NuGetUpgradeTrialOptions options, NuGetRestoreGraphSource baseline, string propsRelative,
        KeyValuePair<string, string>[] selected, List<NuGetTrialStep> steps, List<NuGetTrialPackageEvidence> evidence, CancellationToken ct)
    {
        var root = workspace.Root;
        var solution = baseline.Solution;
        var revision = baseline.Revision;
        var labels = UpdateLabels(selected);
        NuGetRestoreGraphReport? graph = null;
        var candidate = await Step(steps, root, "candidateRestore",
            ["restore", solution, "--force-evaluate", "--no-http-cache", "--disable-build-servers", "-p:RestoreLockedMode=false"],
            ct, updates: labels).ConfigureAwait(false);
        var locked = candidate.ExitCode == 0 && !candidate.AuditUnavailable
            ? await Step(steps, root, "candidateLockedRestore", ["restore", solution, "--locked-mode", "--force", "--no-http-cache", "--disable-build-servers"],
                ct, updates: labels).ConfigureAwait(false)
            : candidate;
        if (candidate.AuditUnavailable || locked.AuditUnavailable)
        {
            return Report(options, revision, steps, evidence, graph, "auditUnavailable", "The final audit source is unavailable.");
        }

        if (options.Vulnerable && (candidate.SourceUnavailable || locked.SourceUnavailable))
        {
            return Report(options, revision, steps, evidence, graph, "candidateUnavailable",
                "An eligible package source was unavailable during the candidate restore.");
        }

        if (locked.ExitCode != 0 && locked.Codes.Count != 0 && locked.Codes.All(IsAdvisory))
        {
            return Report(options, revision, steps, evidence, graph, "unresolvedAdvisories", "The audited candidate still has advisories under the configured policy.");
        }

        if (locked.ExitCode != 0)
        {
            return Report(options, revision, steps, evidence, graph, "blocked", "The proposed version set did not pass the repository restore and audit policy.");
        }

        var restored = await NuGetRestoreGraphComparison.Read(root, null, ct).ConfigureAwait(false);
        var firstFileAppStep = steps.Count;
        var fileAppFailure = await RestoreFileApps(workspace, restored, baseline.FileApps, steps, baselineRestore: false, ct).ConfigureAwait(false);
        graph = NuGetRestoreGraphComparison.Compare(baseline, restored);
        if (fileAppFailure is not null)
        {
            return Report(options, revision, steps, evidence, graph, "blocked", fileAppFailure);
        }

        if (graph.Projects.Any(project => project.BaselineUnavailable is not null || project.CandidateUnavailable is not null))
        {
            return Report(options, revision, steps, evidence, graph, "blocked", "The candidate restore graph is incomplete.");
        }

        if (options.Vulnerable)
        {
            var fileAppSteps = steps.Skip(firstFileAppStep).ToArray();
            if (fileAppSteps.Any(step => step.SourceUnavailable))
            {
                return Report(options, revision, steps, evidence, graph, "candidateUnavailable",
                    "An eligible package source was unavailable during a file-based app restore.");
            }

            if (locked.Codes.Any(IsAdvisory) || fileAppSteps.Any(step =>
                step.Name == CandidateFileAppLockedRestore && step.Codes.Any(IsAdvisory)))
            {
                return Report(options, revision, steps, evidence, graph, "unresolvedAdvisories",
                    "The audited candidate still has advisories under the configured policy.");
            }
        }

        if (!NuGetAuditMinimum.Matches(baseline, restored, selected))
        {
            return Report(options, revision, steps, evidence, graph, "candidateUnavailable",
                "The candidate did not resolve the exact direct versions requested in every affected target.");
        }

        if (options.RemovePin is { } removed
            && graph.Projects.SelectMany(project => project.Changes).Any(change => IsPinDowngrade(change, removed)))
        {
            return Report(options, revision, steps, evidence, graph, "pinRequired", "Retain the pin. The candidate has a lower or unconfirmed dependency floor.");
        }

        if (!await ValidateCandidate(steps, root, solution, options, ct).ConfigureAwait(false))
        {
            return Report(options, revision, steps, evidence, graph, "blocked", "The candidate failed the requested build or test validation.");
        }

        var patch = await workspace.ReviewPatch(propsRelative, ct).ConfigureAwait(false);
        return patch.Length == 0
            ? Report(options, revision, steps, evidence, graph, "noChanges", "No changes were needed.")
            : Report(options, revision, steps, evidence, graph, "verified", "Verified the trial and wrote a reviewable patch.", patch);
    }

    private static async Task<string?> RestoreFileApps(
        NuGetTrialWorkspace workspace, NuGetRestoreGraphSource source, IReadOnlyList<NuGetFileApp> fileApps,
        List<NuGetTrialStep> steps, bool baselineRestore, CancellationToken ct)
    {
        var root = workspace.Root;
        foreach (var app in fileApps)
        {
            var current = baselineRestore ? app : await NuGetFileApps.Evaluate(root, app.Path, ct).ConfigureAwait(false);
            if (current.HasProjectReferences)
            {
                return $"The file-based app {app.Path} has project references that cannot be restored in isolation.";
            }

            var path = NuGetTrialWorkspace.RepositoryPath(root, app.Path);
            var output = workspace.FileAppRestorePath(app.Path);
            List<string> restoreOptions = ["-p:RestoreOutputPath=" + output];
            var scratchLock = await UsesScratchFileAppLock(root, current.LockFile, ct).ConfigureAwait(false);
            if (scratchLock)
            {
                restoreOptions.Add("-p:NuGetLockFilePath=" + Path.Combine(output, "file-app.lock.json"));
            }

            if (!baselineRestore || scratchLock)
            {
                var forceName = baselineRestore ? "baselineFileAppRestore" : "candidateFileAppRestore";
                var forced = await Step(steps, root, forceName,
                    ["restore", path, "--force-evaluate", "--no-http-cache", "--disable-build-servers", "-p:RestoreLockedMode=false", .. restoreOptions],
                    ct, fileApp: current).ConfigureAwait(false);
                if (forced.ExitCode != 0 || forced.AuditUnavailable)
                {
                    return $"The file-based app {app.Path} failed restore.";
                }
            }

            var name = baselineRestore ? "baselineFileAppLockedRestore" : CandidateFileAppLockedRestore;
            var locked = await Step(steps, root, name,
                ["restore", path, "--locked-mode", "--force", "--no-http-cache", "--disable-build-servers", .. restoreOptions],
                ct, fileApp: current)
                .ConfigureAwait(false);
            if (locked.ExitCode != 0 || locked.AuditUnavailable)
            {
                return $"The file-based app {app.Path} failed locked restore.";
            }

            var evidence = await NuGetFileApps.ReadGraph(root, app.Path, [.. restoreOptions], ct).ConfigureAwait(false);
            source.Projects.Add(app.Path, evidence);
            if (evidence.Unavailable is not null)
            {
                return $"The file-based app {app.Path} has incomplete restore evidence.";
            }
        }

        return null;
    }

    private static async Task<bool> UsesScratchFileAppLock(string root, string? lockFile, CancellationToken ct)
    {
        if (lockFile is null)
        {
            return false;
        }

        _ = NuGetTrialWorkspace.RepositoryPath(root, lockFile);
        var tracked = await GitTrackedFiles.Read(root, [":(literal)" + lockFile], ct).ConfigureAwait(false);
        return tracked.Count == 0;
    }

    private static async Task RecordPackageEvidence(
        List<NuGetTrialPackageEvidence> evidence, NuGetTrialWorkspace workspace, string solution, NuGetRestoreGraphSource baseline, string? pin,
        List<NuGetTrialStep> steps, CancellationToken ct)
    {
        var root = workspace.Root;
        var fileAppPaths = baseline.FileApps.Select(app => app.Path).ToHashSet(StringComparer.Ordinal);
        var affected = steps.SelectMany(step => step.Advisories).Select(advisory => advisory.Package)
            .Concat(pin is null ? [] : [pin]).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase);
        foreach (var id in affected)
        {
            var scopes = baseline.Projects.SelectMany(project => (project.Value.Graph?.FindPackage(id) ?? [])
                .Select(match => (Path: project.Key, match.Target, match.Package))).ToArray();
            var selections = scopes.Select(scope =>
                $"{scope.Path} [{scope.Target}]: {scope.Package.Version} ({scope.Package.Classification})").ToArray();
            List<string> dependencyPaths = [];
            if (scopes.Any(scope => !fileAppPaths.Contains(scope.Path)))
            {
                await AddPaths(solution).ConfigureAwait(false);
            }

            foreach (var file in scopes.Select(scope => scope.Path).Where(fileAppPaths.Contains).Distinct(StringComparer.Ordinal))
            {
                var assets = Path.Combine(workspace.FileAppRestorePath(file), "project.assets.json");
                await AddPaths(assets, file).ConfigureAwait(false);
            }

            evidence.Add(new(id, selections, dependencyPaths.Count == 0 ? null : string.Join("\n\n", dependencyPaths)));

            async Task AddPaths(string input, string? file = null)
            {
                var paths = await NuGetRestoreGraphComparison.RunDotnet(root, new BoundedProcessRunner(),
                    ["nuget", "why", input, id], ct).ConfigureAwait(false);
                if (paths.ExitCode == 0)
                {
                    var pathText = paths.StandardOutput.Trim().Replace(root + Path.DirectorySeparatorChar, string.Empty, StringComparison.Ordinal);
                    dependencyPaths.Add(file is null ? pathText : file + "\n" + pathText);
                }
            }
        }
    }

    private static async Task<NuGetTrialStep?> ProposePinOwners(
        List<NuGetTrialStep> steps, string root, string solution, string props, string pin,
        NuGetRestoreGraphSource baseline, IReadOnlyList<string> coordinated, CancellationToken ct)
    {
        if (coordinated.Any(id => !baseline.Projects.Values.Any(project => project.Graph!.FindPackage(id)
            .Any(match => match.Package.Classification == NuGetRestoreClassification.Direct))))
        {
            throw new ArgumentException("Each coordinated package must be a direct dependency in the evaluated solution.");
        }

        var owners = baseline.Projects.Values.SelectMany(project => project.Graph!.FindDirectOwners(pin, version: null))
            .Concat(coordinated).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (owners.Length == 0)
        {
            return null;
        }

        var original = await NuGetTrialProps.ReadBytes(props, ct).ConfigureAwait(false);
        var result = await Step(steps, root, "sdkPinOwnerUpdate",
            ["package", "update", .. owners, "--project", solution], ct, packages: owners).ConfigureAwait(false);
        if (result.ExitCode == 0 && !result.AuditUnavailable)
        {
            await NuGetTrialProps.PreserveFormatting(props, original, ct).ConfigureAwait(false);
        }

        return result;
    }

    private static bool IsAdvisory(string code) => code is "NU1901" or "NU1902" or "NU1903" or "NU1904";

    /// <summary>Checks whether a failed candidate can be ruled out of the ordered search.</summary>
    internal static bool IsConclusiveRejection(string status, NuGetTrialStep last)
    {
        if (status == "pinRequired")
        {
            return true;
        }

        if (last.Name == CandidateFileAppLockedRestore)
        {
            return status == "unresolvedAdvisories" && last.ExitCode == 0;
        }

        if (status is not ("unresolvedAdvisories" or "blocked")
            || last.Name is not ("candidateRestore" or "candidateLockedRestore"))
        {
            return false;
        }

        return last.ExitCode == 0 ? status == "unresolvedAdvisories"
            : last.Errors.Count != 0 && last.Errors.All(code => IsAdvisory(code)
                || code is "NU1107" or "NU1109" or "NU1202" or "NU1605" or "NU1608");
    }

    private static string[] UpdateLabels(IEnumerable<KeyValuePair<string, string>> updates) =>
        updates.Select(pair => pair.Key + "@" + pair.Value).ToArray();

    private static bool IsPinDowngrade(NuGetRestoreChange change, string pin) =>
        string.Equals(change.Package, pin, StringComparison.OrdinalIgnoreCase)
        && change.Before is { } before && change.After is { } after
        && !string.Equals(before.Version, after.Version, StringComparison.OrdinalIgnoreCase)
        && (!Version.TryParse(before.Version.Split('-', '+')[0], out var previousVersion)
            || !Version.TryParse(after.Version.Split('-', '+')[0], out var candidateVersion)
            || candidateVersion <= previousVersion);

    private static async Task<bool> ValidateCandidate(
        List<NuGetTrialStep> steps, string root, string solution, NuGetUpgradeTrialOptions options, CancellationToken ct)
    {
        if (options.Build || options.Test)
        {
            var build = await Step(steps, root, "build",
                ["build", solution, "--no-restore", "--disable-build-servers", "-c", "Release", "-v", "quiet"], ct)
                .ConfigureAwait(false);
            if (build.ExitCode != 0)
            {
                return false;
            }
        }

        return !options.Test || (await Step(steps, root, "test",
            ["test", "--solution", solution, "--no-build", "--no-restore", "-c", "Release", "-v", "quiet"], ct)
            .ConfigureAwait(false)).ExitCode == 0;
    }

    private static async Task<NuGetTrialStep> Step(
        List<NuGetTrialStep> steps, string root, string name, string[] args, CancellationToken ct,
        IReadOnlyList<string>? packages = null, IReadOnlyList<string>? updates = null, NuGetFileApp? fileApp = null)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(Path.GetDirectoryName(root)!, "http-cache"),
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
            ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
        };

        var verification = name is "build" or "test";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(20));
        IProcessRunner runner = verification ? new SystemProcessRunner() : new BoundedProcessRunner();
        ProcessOutput result;
        try
        {
            result = await NuGetRestoreGraphComparison.RunDotnet(root, runner, args, deadline.Token, environment).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The trial verification step exceeded its time budget.", exception);
        }

        var step = NuGetTrialStep.Read(name, root, result, fileApp) with { Packages = packages ?? [], Updates = updates ?? [] };
        steps.Add(step);
        return step;
    }

    private static (NuGetUpgradeTrialReport Report, string? Patch) Report(
        NuGetUpgradeTrialOptions options, string revision, IReadOnlyList<NuGetTrialStep> steps,
        IReadOnlyList<NuGetTrialPackageEvidence> evidence, NuGetRestoreGraphReport? graph,
        string status, string message, string? patch = null)
    {
        var digest = patch is null ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(patch)));
        return (new(1, revision, status, message, options.RemovePin, options.Updates, steps, evidence, graph, digest), patch);
    }
}
