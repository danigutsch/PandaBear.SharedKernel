using Terminal.Gui.Input;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>A key of the review UI and what it does.</summary>
/// <param name="Key">Key.</param>
/// <param name="Area">Where the key works.</param>
/// <param name="Action">What the key does.</param>
internal sealed record ReviewKey(Key Key, ReviewArea Area, string Action);
