namespace SharedKernel.Versioning;

/// <summary>
/// Represents a Semantic Versioning 2.0.0 version core with optional prerelease and build metadata.
/// </summary>
/// <param name="Major">The major compatibility number.</param>
/// <param name="Minor">The minor feature number.</param>
/// <param name="Patch">The patch fix number.</param>
/// <param name="Prerelease">The optional prerelease identifier.</param>
/// <param name="BuildMetadata">The optional build metadata identifier.</param>
public sealed record SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    string? Prerelease = null,
    string? BuildMetadata = null)
{
    /// <summary>
    /// Parses a stable Semantic Versioning value.
    /// </summary>
    /// <param name="value">The version text.</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is not a supported semantic version.</exception>
    public static SemanticVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var metadataStart = value.IndexOf('+', StringComparison.Ordinal);
        var buildMetadata = metadataStart >= 0 ? value[(metadataStart + 1)..] : null;
        var withoutMetadata = metadataStart >= 0 ? value[..metadataStart] : value;
        var prereleaseStart = withoutMetadata.IndexOf('-', StringComparison.Ordinal);
        var prerelease = prereleaseStart >= 0 ? withoutMetadata[(prereleaseStart + 1)..] : null;
        var core = prereleaseStart >= 0 ? withoutMetadata[..prereleaseStart] : withoutMetadata;
        var parts = core.Split('.');

        if (parts.Length != 3 ||
            !TryParsePart(parts[0], out var major) ||
            !TryParsePart(parts[1], out var minor) ||
            !TryParsePart(parts[2], out var patch))
        {
            throw new ArgumentException("The version must use '<major>.<minor>.<patch>'.", nameof(value));
        }

        return new SemanticVersion(major, minor, patch, prerelease, buildMetadata);
    }

    /// <summary>
    /// Returns the stable version core without prerelease or build metadata.
    /// </summary>
    public SemanticVersion StableCore => this with { Prerelease = null, BuildMetadata = null };

    /// <summary>
    /// Applies a release impact to this version.
    /// </summary>
    /// <param name="impact">The release impact.</param>
    /// <returns>The bumped version.</returns>
    public SemanticVersion Bump(ReleaseImpact impact) => impact switch
    {
        ReleaseImpact.Major => new SemanticVersion(Major + 1, 0, 0),
        ReleaseImpact.Minor => new SemanticVersion(Major, Minor + 1, 0),
        ReleaseImpact.Patch => new SemanticVersion(Major, Minor, Patch + 1),
        _ => StableCore,
    };

    /// <inheritdoc />
    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";

        if (!string.IsNullOrWhiteSpace(Prerelease))
        {
            version += $"-{Prerelease}";
        }

        if (!string.IsNullOrWhiteSpace(BuildMetadata))
        {
            version += $"+{BuildMetadata}";
        }

        return version;
    }

    private static bool TryParsePart(string value, out int result)
    {
        result = 0;
        if (value.Length == 0 || value.Length > 1 && value[0] == '0')
        {
            return false;
        }

        return int.TryParse(value, out result) && result >= 0;
    }
}
