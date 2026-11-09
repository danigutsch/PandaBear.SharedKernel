namespace PandaBear.Packages;

/// <summary>What <see cref="UpdatePlanner.FindUpdates"/> found: the updates, and the packages that could not be checked.</summary>
public sealed class UpdateSearch
{
    /// <summary>Creates a search result.</summary>
    /// <param name="updates">The packages whose newest stable version differs from the installed one.</param>
    /// <param name="notOnRegistry">The packages that the registry does not know, so nothing could be said about them.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public UpdateSearch(IReadOnlyList<PackageUpdate> updates, IReadOnlyList<PackageName> notOnRegistry)
    {
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(notOnRegistry);
        Updates = updates;
        NotOnRegistry = notOnRegistry;
    }

    /// <summary>Gets the packages whose newest stable version differs from the installed one, ordered by name.</summary>
    public IReadOnlyList<PackageUpdate> Updates
    {
        get;
    }

    /// <summary>
    /// Gets the packages that the registry does not know (a crate from git or a path, a linked npm package, a package with no
    /// stable version), ordered by name. They are not checked: this is never an allow.
    /// </summary>
    public IReadOnlyList<PackageName> NotOnRegistry
    {
        get;
    }
}
