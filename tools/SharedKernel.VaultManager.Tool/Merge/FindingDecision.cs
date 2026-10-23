namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Whether a secret finding is moved to its proposed target.</summary>
internal enum FindingDecision
{
    /// <summary>The finding is applied, also when it was not pre-accepted.</summary>
    Accepted = 0,

    /// <summary>The finding is not applied, also when it was pre-accepted.</summary>
    Rejected = 1,
}
