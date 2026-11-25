namespace SharedKernel.Packages;

/// <summary>
/// The sources that <see cref="PackageInventory.CheckUpdates"/> asks, and the clock it decides at. Disposing it releases
/// the object it owns, such as the HTTP client of <see cref="CreateDefault"/>.
/// </summary>
public sealed class UpdateSources : IDisposable
{
    private readonly IDisposable? _owned;

    /// <summary>Creates the sources.</summary>
    /// <param name="advisories">Supplies advisories.</param>
    /// <param name="releaseDates">Supplies release times.</param>
    /// <param name="latest">Supplies the newest stable versions.</param>
    /// <param name="time">The clock.</param>
    /// <param name="owned">Disposed with this object, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A source or <paramref name="time"/> is <see langword="null"/>.</exception>
    public UpdateSources(IAdvisorySource advisories, IReleaseDateSource releaseDates, ILatestVersionSource latest, TimeProvider time, IDisposable? owned = null)
    {
        ArgumentNullException.ThrowIfNull(advisories);
        ArgumentNullException.ThrowIfNull(releaseDates);
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(time);
        Advisories = advisories;
        ReleaseDates = releaseDates;
        Latest = latest;
        Time = time;
        _owned = owned;
    }

    /// <summary>Gets the advisory source.</summary>
    public IAdvisorySource Advisories { get; }

    /// <summary>Gets the release-date source.</summary>
    public IReleaseDateSource ReleaseDates { get; }

    /// <summary>Gets the latest-version source.</summary>
    public ILatestVersionSource Latest { get; }

    /// <summary>Gets the clock.</summary>
    public TimeProvider Time { get; }

    /// <summary>
    /// Creates the real sources: <see cref="OsvAdvisorySource"/> for advisories and <see cref="RegistryReleaseSource"/>
    /// (crates.io and the npm registry) for versions and dates, over one HTTP client built with
    /// <see cref="PackageHttp.CreateHandler"/> and the system clock. Nothing is contacted until a source is asked.
    /// </summary>
    /// <returns>The sources; the caller disposes them.</returns>
    public static UpdateSources CreateDefault()
    {
        // The client owns the handler, and the returned object owns the client.
#pragma warning disable CA2000
        var client = new HttpClient(PackageHttp.CreateHandler());
#pragma warning restore CA2000
        var registry = new RegistryReleaseSource(client, TimeProvider.System);
        return new UpdateSources(new OsvAdvisorySource(client, TimeProvider.System), registry, registry, TimeProvider.System, client);
    }

    /// <inheritdoc />
    public void Dispose() => _owned?.Dispose();
}
