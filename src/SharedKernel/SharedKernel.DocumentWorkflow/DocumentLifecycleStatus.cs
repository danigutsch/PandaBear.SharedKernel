namespace SharedKernel.DocumentWorkflow;

/// <summary>Identifies the lifecycle phase of one document revision.</summary>
public enum DocumentLifecycleStatus
{
    /// <summary>No lifecycle state has been assigned.</summary>
    None = 0,
    /// <summary>A generated draft is available to review.</summary>
    DraftGenerated = 1,
    /// <summary>Human review is active.</summary>
    InReview = 2,
    /// <summary>A reviewer requested changes.</summary>
    ChangesRequested = 3,
    /// <summary>All required review lanes approved the current content.</summary>
    Approved = 4,
    /// <summary>The revision references its finalized artifact.</summary>
    Finalized = 5,
    /// <summary>A newer revision replaced this one.</summary>
    Superseded = 6,
    /// <summary>The revision was withdrawn from use.</summary>
    Voided = 7,
}
