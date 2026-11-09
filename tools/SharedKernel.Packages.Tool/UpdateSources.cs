namespace PandaBear.Packages.Tool;

/// <summary>The network sources that <c>updates</c> uses, and the clock; disposing it releases the HTTP client it owns.</summary>
/// <param name="advisories">Supplies advisories.</param>
/// <param name="releaseDates">Supplies release times.</param>
/// <param name="latest">Supplies the newest stable versions.</param>
/// <param name="time">The clock.</param>
/// <param name="owned">Disposed with this object, or <see langword="null"/>.</param>
internal sealed class UpdateSources(IAdvisorySource advisories, IReleaseDateSource releaseDates, ILatestVersionSource latest, TimeProvider time, IDisposable? owned = null) : IDisposable
{
    /// <summary>Gets the advisory source.</summary>
    public IAdvisorySource Advisories
    {
        get;
    } = advisories;

    /// <summary>Gets the release-date source.</summary>
    public IReleaseDateSource ReleaseDates
    {
        get;
    } = releaseDates;

    /// <summary>Gets the latest-version source.</summary>
    public ILatestVersionSource Latest
    {
        get;
    } = latest;

    /// <summary>Gets the clock.</summary>
    public TimeProvider Time
    {
        get;
    } = time;

    /// <summary>Creates the real sources: OSV for advisories, crates.io and the npm registry for versions and dates.</summary>
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
    public void Dispose() => owned?.Dispose();
}
