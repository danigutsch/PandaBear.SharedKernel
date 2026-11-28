namespace SharedKernel.Packages.Tool;

/// <summary>The observations displayed in the package review window and its optional declaration edit.</summary>
/// <param name="Declared">The requirements from the repository configuration.</param>
/// <param name="Plan">Differences from the installed packages or the failure to observe them.</param>
/// <param name="Updates">Update decisions when the caller allowed network access.</param>
/// <param name="Edit">The repository configuration snapshot for an interactive edit.</param>
internal sealed record PackageReviewSession(
    DeclaredPackages Declared, PackagePlanReport Plan, PackageUpdatesReport? Updates, PackageConfigurationEdit? Edit = null)
{
    /// <summary>Gets or sets the validated file contents selected by explicit confirmation.</summary>
    internal byte[]? ConfirmedEdit { get; set; }

    /// <summary>Reports complete, drifted, or incomplete observations with the CLI exit codes.</summary>
    internal int ExitCode => !Plan.IsComplete || Updates is { HasFailure: true } or { HasSkipped: true }
        ? PlanCommand.FailureExitCode
        : Plan.ChangeCount > 0 ? PlanCommand.DifferencesExitCode : 0;

    /// <summary>Builds readable declared requirements and update reasons for one manager.</summary>
    internal IReadOnlyList<string> DetailsFor(PackageManagerKind manager)
    {
        var managerPlan = Plan.Managers.FirstOrDefault(plan => plan.Manager == manager);
        var changes = managerPlan?.Changes.ToDictionary(static change => change.Requirement.Name);
        List<string> lines = ["Declared packages"];
        if (managerPlan?.Error is { } error)
        {
            lines.Add(error);
        }
        else if (Plan.MissingTools.Count > 0)
        {
            lines.Add($"Installed state unavailable. Missing on PATH: {string.Join(", ", Plan.MissingTools.Select(static tool => tool.Executable))}.");
        }

        foreach (var requirement in Declared.For(manager))
        {
            if (changes?.TryGetValue(requirement.Name, out var change) == true)
            {
                lines.Add(change.ToString());
            }
            else
            {
                var status = managerPlan is { Error: null } ? "installed as declared" : "installed state unknown";
                lines.Add($"{requirement}: {status}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Update decisions");
        AddUpdates(manager, lines);
        return lines;
    }

    private void AddUpdates(PackageManagerKind manager, List<string> lines)
    {
        if (Updates is null)
        {
            lines.Add("Use --allow-network to check for updates.");
            return;
        }

        if (Updates.Unsupported.Contains(manager))
        {
            lines.Add("Updates are unavailable for this manager.");
            return;
        }

        if (Updates.MissingTools.Count > 0)
        {
            lines.Add($"Update state unavailable. Missing on PATH: {string.Join(", ", Updates.MissingTools.Select(static tool => tool.Executable))}.");
            return;
        }

        var report = Updates.Managers.FirstOrDefault(result => result.Manager == manager);
        if (report?.Error is { } error)
        {
            lines.Add(error);
            return;
        }

        if (report is null)
        {
            lines.Add("No updates were checked.");
            return;
        }

        lines.AddRange(report.Pinned.Select(static requirement => $"{requirement}: pinned."));
        lines.AddRange(report.NotOnRegistry.Select(static package => $"{package}: not found in the registry."));
        lines.AddRange(report.Decisions.Select(static decision => decision.ToString()));
        if (report.Decisions.Count == 0)
        {
            lines.Add("No update decisions for the declared packages.");
        }
    }
}
