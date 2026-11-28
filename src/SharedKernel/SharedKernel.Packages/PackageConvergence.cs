namespace SharedKernel.Packages;

/// <summary>Reconciles a declaration against live observations through caller-owned execution ports.</summary>
public sealed class PackageConvergence
{
    private readonly PackageInventory _inventory;

    /// <summary>Creates a convergence engine.</summary>
    /// <param name="inventory">Reads installed state and plans changes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inventory"/> is <see langword="null"/>.</exception>
    public PackageConvergence(PackageInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        _inventory = inventory;
    }

    /// <summary>Previews the current plan or explicitly applies it and checks the installed state again.</summary>
    /// <param name="desired">The desired packages grouped by owning manager.</param>
    /// <param name="apply">Enables application of the freshly observed plan.</param>
    /// <param name="ports">Execution and authorization ports required when applying.</param>
    /// <param name="cancellationToken">Cancels observation or stops before the next change.</param>
    /// <returns>The plan and observed outcome of the requested operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="desired"/> is null or apply was requested without ports.</exception>
    public async Task<PackageConvergenceReport> Run(
        DeclaredPackages desired, bool apply = false, PackageApplyPorts? ports = null, CancellationToken cancellationToken = default)
    {
        var selected = apply ? ports ?? throw new ArgumentNullException(nameof(ports)) : null;
        var plan = await _inventory.Plan(desired, cancellationToken).ConfigureAwait(false);
        if (selected is null || !plan.IsComplete)
        {
            return new PackageConvergenceReport(plan, apply, [], null);
        }

        List<PackageChangeExecution> changes = [];
        foreach (var manager in plan.Managers)
        {
            foreach (var change in manager.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                changes.Add(await ApplyChange(selected, manager.Manager, change, cancellationToken).ConfigureAwait(false));
            }
        }

        var remaining = changes.Count == 0 ? plan : await _inventory.Plan(desired, cancellationToken).ConfigureAwait(false);
        return new PackageConvergenceReport(plan, apply, changes, remaining);
    }

    private static async Task<PackageChangeExecution> ApplyChange(
        PackageApplyPorts ports, PackageManagerKind manager, PackageChange change, CancellationToken cancellationToken)
    {
        PackageChangePrivilege? privilege = null;
        try
        {
            privilege = ports.Classify(manager, change);
            IPackageChangeExecutor executor;
            switch (privilege)
            {
                case PackageChangePrivilege.Unprivileged:
                    executor = ports.Unprivileged;
                    break;
                case PackageChangePrivilege.Elevated:
                    if (ports.Elevated is not { } elevated)
                    {
                        return new PackageChangeExecution(manager, change, privilege, PackageChangeOutcome.ElevationUnavailable,
                            "This change requires an elevated execution port.");
                    }

                    if (ports.AuthorizeElevation?.Invoke(manager, change) != true)
                    {
                        return new PackageChangeExecution(manager, change, privilege, PackageChangeOutcome.ElevationDenied,
                            "Elevation was not approved for this change.");
                    }

                    executor = elevated;
                    break;
                default:
                    return new PackageChangeExecution(manager, change, PackageChangePrivilege.Unsupported,
                        PackageChangeOutcome.Unsupported, "The configured backend does not support this change.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = await executor.Apply(manager, change, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? new PackageChangeExecution(manager, change, privilege, PackageChangeOutcome.Applied, null)
                : new PackageChangeExecution(manager, change, privilege, PackageChangeOutcome.Failed, result.ErrorDetails?.Detail);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            return new PackageChangeExecution(manager, change, privilege, PackageChangeOutcome.Failed, exception.Message);
        }
    }
}
