namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The approved third-party Actions that workflows and composite actions may reference.
/// </summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="FirstParty">Repositories whose references need a full commit SHA but no manifest entry or version comment.</param>
/// <param name="Actions">The approved Actions.</param>
internal sealed record ActionPinManifest(int Version, string[] FirstParty, ActionPin[] Actions);
