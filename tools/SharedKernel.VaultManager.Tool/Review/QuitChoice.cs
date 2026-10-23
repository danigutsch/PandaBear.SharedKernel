namespace PandaBear.VaultManager.Tool.Review;

/// <summary>What to do with unsaved decisions when the user quits.</summary>
internal enum QuitChoice
{
    /// <summary>Keep the review open.</summary>
    Cancel = 0,

    /// <summary>Quit without saving.</summary>
    Discard,

    /// <summary>Save the decisions, then quit.</summary>
    Save,
}
