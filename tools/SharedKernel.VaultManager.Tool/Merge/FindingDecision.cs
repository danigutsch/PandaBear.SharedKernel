namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// Whether a secret finding is moved to its proposed target. The zero value is <see cref="Pending"/>, so an unset
/// decision never applies a finding.
/// </summary>
internal enum FindingDecision
{
    /// <summary>
    /// No decision yet; it counts as no decision. A pre-accepted finding is applied, and a proposed finding is not.
    /// </summary>
    Pending = 0,

    /// <summary>The finding is applied, also when it was not pre-accepted.</summary>
    Accepted = 1,

    /// <summary>The finding is not applied, also when it was pre-accepted.</summary>
    Rejected = 2,
}
