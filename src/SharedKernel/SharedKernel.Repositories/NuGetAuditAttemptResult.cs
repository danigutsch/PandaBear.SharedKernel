namespace PandaBear.Repositories;

/// <summary>How a trial outcome affects the proof of a minimum.</summary>
internal enum NuGetAuditAttemptResult
{
    /// <summary>The SDK conclusively rejected this exact candidate.</summary>
    Rejected,

    /// <summary>The exact candidate passed the original repository policy.</summary>
    Verified,

    /// <summary>The SDK or workspace did not establish whether the candidate works.</summary>
    Unavailable,
}
