namespace SharedKernel.ApiVersioning;

/// <summary>
/// Provides selection over known API contract versions.
/// </summary>
public sealed class ApiVersionCatalog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiVersionCatalog"/> class.
    /// </summary>
    /// <param name="versions">The known API contract versions.</param>
    /// <exception cref="ArgumentException">Thrown when versions are empty or contain duplicates.</exception>
    public ApiVersionCatalog(IEnumerable<ApiVersionDefinition> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);

        Versions = [.. versions.OrderByDescending(static item => item.Version)];
        if (Versions.Count == 0)
        {
            throw new ArgumentException("At least one API version is required.", nameof(versions));
        }

        if (Versions.Select(static item => item.Version).Distinct().Count() != Versions.Count)
        {
            throw new ArgumentException("Duplicate API versions are not allowed.", nameof(versions));
        }
    }

    /// <summary>
    /// Gets the known API contract versions in descending version order.
    /// </summary>
    public IReadOnlyList<ApiVersionDefinition> Versions { get; }

    /// <summary>
    /// Gets versions that are available for request selection.
    /// </summary>
    public IReadOnlyList<ApiVersionDefinition> SelectableVersions => [.. Versions.Where(static item => item.Status != ApiVersionStatus.Retired)];

    /// <summary>
    /// Selects the requested API version, or the latest selectable version when no version is requested.
    /// </summary>
    /// <param name="requestedVersion">The requested API version.</param>
    /// <returns>The selected API version definition.</returns>
    /// <exception cref="ArgumentException">Thrown when the requested version is unknown or retired.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no selectable version exists.</exception>
    public ApiVersionDefinition Select(ApiVersion? requestedVersion = null)
    {
        if (requestedVersion is null)
        {
            foreach (ApiVersionDefinition version in Versions)
            {
                if (version.Status != ApiVersionStatus.Retired)
                {
                    return version;
                }
            }

            throw new InvalidOperationException("At least one non-retired API version is required.");
        }

        ApiVersionDefinition? selected = Versions.FirstOrDefault(item => item.Version == requestedVersion.Value) ?? throw new ArgumentException($"API version '{requestedVersion}' is not supported.", nameof(requestedVersion));
        return selected.Status == ApiVersionStatus.Retired
            ? throw new ArgumentException($"API version '{requestedVersion}' is retired.", nameof(requestedVersion))
            : selected;
    }
}
