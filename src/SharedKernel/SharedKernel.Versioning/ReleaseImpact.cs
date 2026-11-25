namespace SharedKernel.Versioning;

/// <summary>
/// Describes the release impact produced by a change.
/// </summary>
public enum ReleaseImpact
{
    /// <summary>
    /// No released artifact changes.
    /// </summary>
    None = 0,

    /// <summary>
    /// A backward-compatible bug fix.
    /// </summary>
    Patch = 1,

    /// <summary>
    /// A backward-compatible feature.
    /// </summary>
    Minor = 2,

    /// <summary>
    /// A breaking change.
    /// </summary>
    Major = 3,
}
