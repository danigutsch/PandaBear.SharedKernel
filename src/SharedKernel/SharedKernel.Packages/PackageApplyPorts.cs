namespace SharedKernel.Packages;

/// <summary>Supplies the execution boundaries and the caller's privilege decisions for an apply run.</summary>
public sealed class PackageApplyPorts
{
    /// <summary>Creates the ports used by an explicit application.</summary>
    /// <param name="unprivileged">Executes changes that need no elevation.</param>
    /// <param name="classify">Classifies each change for the configured backend without changing package state.</param>
    /// <param name="elevated">Executes a single explicitly approved elevated change.</param>
    /// <param name="authorizeElevation">Approves elevation for the exact manager and change.</param>
    /// <exception cref="ArgumentNullException">The standard executor or classifier is <see langword="null"/>.</exception>
    public PackageApplyPorts(
        IPackageChangeExecutor unprivileged,
        Func<PackageManagerKind, PackageChange, PackageChangePrivilege> classify,
        IPackageChangeExecutor? elevated = null,
        Func<PackageManagerKind, PackageChange, bool>? authorizeElevation = null)
    {
        ArgumentNullException.ThrowIfNull(unprivileged);
        ArgumentNullException.ThrowIfNull(classify);
        Unprivileged = unprivileged;
        Classify = classify;
        Elevated = elevated;
        AuthorizeElevation = authorizeElevation;
    }

    /// <summary>Gets the standard execution port.</summary>
    internal IPackageChangeExecutor Unprivileged { get; }

    /// <summary>Gets the caller's classifier for one change.</summary>
    internal Func<PackageManagerKind, PackageChange, PackageChangePrivilege> Classify { get; }

    /// <summary>Gets the optional elevated port.</summary>
    internal IPackageChangeExecutor? Elevated { get; }

    /// <summary>Gets the optional per-change elevation approval.</summary>
    internal Func<PackageManagerKind, PackageChange, bool>? AuthorizeElevation { get; }
}
