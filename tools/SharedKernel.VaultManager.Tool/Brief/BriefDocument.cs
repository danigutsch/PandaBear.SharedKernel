using PandaBear.VaultManager.Tool.Matching;

namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>
/// On-disk shape of a merge brief: what an agent needs to propose decisions for a merge, without secret values. It lists
/// the groups that need a decision with the user's decisions, their members and secret findings, and the fields on which
/// the members differ.
/// </summary>
/// <param name="Version">Format version; <see cref="BriefFile.CurrentVersion"/>.</param>
/// <param name="Inputs">
/// File names of the inputs the brief was made from, in order. The brief holds no hash of them: an unencrypted export's
/// hash would let a reader test guesses of the passwords it holds.
/// </param>
/// <param name="Summary">Counts of the merge.</param>
/// <param name="Groups">Groups that need a decision, in group order.</param>
/// <param name="Suggestions">Weak matches that were not applied. Only the user can link items.</param>
internal sealed record BriefDocument(
    int Version,
    IReadOnlyList<string> Inputs,
    BriefSummary Summary,
    IReadOnlyList<BriefGroup> Groups,
    IReadOnlyList<MatchSuggestion> Suggestions);
