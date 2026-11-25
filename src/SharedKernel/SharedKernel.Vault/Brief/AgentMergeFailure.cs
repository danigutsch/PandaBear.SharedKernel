namespace SharedKernel.Vault.Brief;

/// <summary>The validation stage that rejected an agent merge preview.</summary>
public enum AgentMergeFailure
{
    /// <summary>The scanned file names or their order differ from the saved brief.</summary>
    InputFiles,

    /// <summary>The agent proposed a decision outside the saved brief's limits.</summary>
    SavedBrief,

    /// <summary>A decision no longer fits the scanned vault and the user's current decisions.</summary>
    CurrentInputs,

    /// <summary>The proposed decisions change what the brief describes or create a conflict.</summary>
    ChangedMerge,
}
