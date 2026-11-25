namespace PandaBear.Review;

/// <summary>Identifies the recorded decision on a content revision.</summary>
public enum ReviewDecision
{
    /// <summary>No decision was supplied.</summary>
    None = 0,

    /// <summary>A human approved the current lane.</summary>
    Approved = 1,

    /// <summary>A human requested a new content revision.</summary>
    ChangesRequested = 2,

    /// <summary>An automated reviewer recorded a non-authoritative finding.</summary>
    Advisory = 3,
}
