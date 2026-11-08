namespace PandaBear.Packages;

/// <summary>What the policy says about one update candidate.</summary>
public enum UpdateDecisionKind
{
    /// <summary>The candidate is old enough and not affected by an advisory.</summary>
    Allow,

    /// <summary>The candidate is not old enough yet.</summary>
    Hold,

    /// <summary>The installed version has an advisory that the candidate does not: update now, whatever the age.</summary>
    Urgent,

    /// <summary>The candidate is affected by an advisory and must not be installed.</summary>
    Block,
}
