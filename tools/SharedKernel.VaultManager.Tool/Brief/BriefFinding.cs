using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>A secret finding of a member, described by its location, never by its value.</summary>
/// <param name="Id">Finding identifier, which finding decisions name.</param>
/// <param name="Kind">Kind of secret.</param>
/// <param name="Confidence">How certain the finding is.</param>
/// <param name="FieldPath">Field the finding lies in, such as <c>notes</c> or <c>fields[2].value</c>.</param>
/// <param name="Line">1-based line of the field text where the finding starts, or <see langword="null"/> when unknown.</param>
/// <param name="ProposedTarget">Where accepting the finding moves it.</param>
/// <param name="PreAccepted">Whether the finding is applied unless a decision rejects it.</param>
/// <param name="NeedsDecision">Whether the finding waits for an accept or reject decision.</param>
/// <param name="AgentMayAccept">
/// Whether an agent decisions file may accept the finding. An agent decides without seeing the value, so it may not
/// accept a finding that a person must review, that moves text into a place that catalogs and briefs show, such as the user
/// name, or that changes which items match.
/// </param>
/// <param name="AgentMayReject">
/// Whether an agent decisions file may reject the finding: not when the finding is pre-accepted and rejecting it changes
/// which items match.
/// </param>
internal sealed record BriefFinding(
    string Id,
    SecretKind Kind,
    Confidence Confidence,
    string FieldPath,
    int? Line,
    string ProposedTarget,
    bool PreAccepted,
    bool NeedsDecision,
    bool AgentMayAccept,
    bool AgentMayReject);
