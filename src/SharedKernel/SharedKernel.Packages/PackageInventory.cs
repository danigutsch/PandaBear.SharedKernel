using SharedKernel.Processes;

namespace SharedKernel.Packages;

/// <summary>
/// Compares declared packages with what each package manager has installed, and judges the newest stable versions of
/// the installed ones. It reads only: it runs each manager's listing program and never installs, removes, or updates.
/// </summary>
public sealed class PackageInventory
{
    private readonly IProcessRunner _runner;
    private readonly IExecutableResolver _executables;

    /// <summary>Creates an inventory.</summary>
    /// <param name="runner">Runs the listing programs; hosts pass <see cref="BoundedProcessRunner"/>.</param>
    /// <param name="executables">Finds each listing program by name and returns its absolute path.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public PackageInventory(IProcessRunner runner, IExecutableResolver executables)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(executables);
        _runner = runner;
        _executables = executables;
    }

    /// <summary>
    /// Plans the declared packages: what is missing or installed at another version than an exact requirement. Every
    /// program is found first, and when any is missing nothing is run. A manager whose installed packages cannot be read
    /// gets an error and the others are still planned.
    /// </summary>
    /// <param name="declared">The declared packages.</param>
    /// <param name="cancellationToken">Cancels the run; cancellation throws.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="declared"/> is <see langword="null"/>.</exception>
    public async Task<PackagePlanReport> Plan(DeclaredPackages declared, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);
        cancellationToken.ThrowIfCancellationRequested();
        var (readers, missing) = Resolve(declared.Managers);
        if (missing.Count > 0)
        {
            return new PackagePlanReport(missing, []);
        }

        List<ManagerPlan> managers = [];
        foreach (var (manager, reader) in readers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requirements = declared.For(manager);
            var installed = await reader.ListInstalled(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            managers.Add(installed.IsFailure
                ? new ManagerPlan(manager, requirements.Count, [], installed.ErrorDetails?.Detail ?? "could not read the installed packages.")
                : new ManagerPlan(manager, requirements.Count, PackagePlanner.Plan(requirements, installed.Value), null));
        }

        return new PackagePlanReport([], managers);
    }

    /// <summary>
    /// Judges the newest stable version of each declared, installed package that is not pinned to an exact version
    /// against advisories and <paramref name="minimumAge"/>. Only cargo and npm have release dates; other managers are
    /// listed as unsupported. Every program is found first, and when any is missing nothing is run or contacted;
    /// <paramref name="createSources"/> is called only when there is a package manager to check, and the sources are
    /// disposed before this returns. A manager whose installed list or lookups fail gets an error and no decisions, and
    /// the others are still judged.
    /// </summary>
    /// <param name="declared">The declared packages.</param>
    /// <param name="minimumAge">How long a release must have been public.</param>
    /// <param name="access">Whether the sources may contact the network; <see cref="NetworkAccess.Denied"/> fails every lookup.</param>
    /// <param name="createSources">Creates the sources; hosts pass <see cref="UpdateSources.CreateDefault"/>.</param>
    /// <param name="cancellationToken">Cancels the run; cancellation throws.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    public async Task<PackageUpdatesReport> CheckUpdates(
        DeclaredPackages declared,
        MinimumReleaseAge minimumAge,
        NetworkAccess access,
        Func<UpdateSources> createSources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(minimumAge);
        ArgumentNullException.ThrowIfNull(createSources);

        List<PackageManagerKind> supported = [];
        List<PackageManagerKind> unsupported = [];
        foreach (var manager in declared.Managers)
        {
            (HasReleaseDates(manager) ? supported : unsupported).Add(manager);
        }

        var (readers, missing) = Resolve(supported);
        if (missing.Count > 0 || readers.Count == 0)
        {
            return new PackageUpdatesReport(unsupported, missing, []);
        }

        using var sources = createSources();
        var planner = new UpdatePlanner(sources.Advisories, sources.ReleaseDates, sources.Latest, sources.Time);
        List<ManagerUpdates> managers = [];
        foreach (var (manager, reader) in readers)
        {
            managers.Add(await CheckManager(manager, reader, declared.For(manager), planner, minimumAge, access, cancellationToken).ConfigureAwait(false));
        }

        return new PackageUpdatesReport(unsupported, [], managers);
    }

    private static bool HasReleaseDates(PackageManagerKind manager) => manager is PackageManagerKind.Cargo or PackageManagerKind.Npm;

    private static async Task<ManagerUpdates> CheckManager(
        PackageManagerKind manager,
        IPackageReader reader,
        IReadOnlyList<PackageRequirement> declared,
        UpdatePlanner planner,
        MinimumReleaseAge minimumAge,
        NetworkAccess access,
        CancellationToken cancellationToken)
    {
        var installed = await reader.ListInstalled(cancellationToken).ConfigureAwait(false);
        if (installed.IsFailure)
        {
            return new ManagerUpdates(manager, [], [], [], 0, installed.ErrorDetails.Detail);
        }

        var requirements = declared.ToDictionary(static r => r.Name);
        List<PackageRequirement> pinned = [];
        List<InstalledPackage> toCheck = [];
        foreach (var package in installed.Value.Where(p => requirements.ContainsKey(p.Name)))
        {
            var requirement = requirements[package.Name];
            if (requirement.Version is not null)
            {
                pinned.Add(requirement);
            }
            else
            {
                toCheck.Add(package);
            }
        }

        var found = await planner.FindUpdates(manager, toCheck, access, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return new ManagerUpdates(manager, pinned, [], [], 0, found.ErrorDetails.Detail);
        }

        var notOnRegistry = found.Value.NotOnRegistry;
        var decisions = await planner.Plan(manager, found.Value.Updates, minimumAge, access, cancellationToken).ConfigureAwait(false);
        return decisions.IsFailure
            ? new ManagerUpdates(manager, pinned, notOnRegistry, [], 0, decisions.ErrorDetails.Detail)
            : new ManagerUpdates(manager, pinned, notOnRegistry, decisions.Value, toCheck.Count - notOnRegistry.Count, null);
    }

    private (List<(PackageManagerKind Manager, IPackageReader Reader)> Readers, List<MissingTool> Missing) Resolve(IEnumerable<PackageManagerKind> managers)
    {
        List<(PackageManagerKind Manager, IPackageReader Reader)> readers = [];
        List<MissingTool> missing = [];
        foreach (var manager in managers)
        {
            var executable = PackageReaders.Executable(manager);
            var path = _executables.Find(executable);
            if (path is null)
            {
                missing.Add(new MissingTool(manager, executable));
            }
            else
            {
                readers.Add((manager, PackageReaders.Create(manager, _runner, path)));
            }
        }

        return (readers, missing);
    }
}
