using PandaBear.VaultManager.Tool.Brief;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Brief;

/// <summary>
/// A brief with one group of two logins: their passwords need a decision, only the first has website addresses, and the
/// first holds a pre-accepted finding and two findings that need a decision, one of which an agent may not accept; the
/// second holds a pre-accepted finding that an agent may not reject.
/// </summary>
internal static class BriefSamples
{
    public const string GroupKey = "4943740a016f2549";
    public const string First = "keeper|keeper.json|rec-1";
    public const string Second = "bitwarden|bitwarden.json|bw-1";
    public const string PreAccepted = "notes-recovery-code@notes:22";
    public const string Proposed = "notes-password@notes:10";

    public const string Username = "notes-username@notes:30";
    public const string PrivateKey = "private-key@notes:0";

    public static BriefDocument Document() => new(
        BriefFile.CurrentVersion,
        ["keeper.json", "bitwarden.json"],
        new BriefSummary(1, 1, 0, 1, 3, 1, 2, 0, 0),
        [
            new BriefGroup(
                GroupKey,
                VaultItemType.Login,
                [
                    new BriefMember(First, "keeper.json", "Example", null, "example.com", "alice", null, Newest: false,
                    [
                        new BriefFinding(PreAccepted, SecretKind.RecoveryCodes, Confidence.High, SecretLocation.Notes, 2, "hidden field 'Recovery codes'", PreAccepted: true, NeedsDecision: false, AgentMayAccept: true, AgentMayReject: true),
                        new BriefFinding(Proposed, SecretKind.Password, Confidence.Medium, SecretLocation.Notes, 1, "login.password", PreAccepted: false, NeedsDecision: true, AgentMayAccept: true, AgentMayReject: true),
                        new BriefFinding(Username, SecretKind.Username, Confidence.Medium, SecretLocation.Notes, 3, "login.username", PreAccepted: false, NeedsDecision: true, AgentMayAccept: false, AgentMayReject: true),
                    ]),
                    new BriefMember(Second, "bitwarden.json", "Example", null, null, "alice", null, Newest: false,
                    [
                        new BriefFinding(PrivateKey, SecretKind.PrivateKey, Confidence.High, SecretLocation.Notes, 1, "new SSH key item", PreAccepted: true, NeedsDecision: false, AgentMayAccept: true, AgentMayReject: false),
                    ]),
                ],
                [
                    new BriefField(FieldPaths.LoginPassword, NeedsDecision: true, AgentMayDecide: true,
                        [new BriefHolder(First, Newest: false), new BriefHolder(Second, Newest: false)]),
                    new BriefField(FieldPaths.LoginTotp, NeedsDecision: false, AgentMayDecide: true, [new BriefHolder(First, Newest: false)]),
                    new BriefField(FieldPaths.LoginUris, NeedsDecision: false, AgentMayDecide: false,
                        [new BriefHolder(First, Newest: false, Values: ["https://example.com"])]),
                ]),
        ],
        []);

    /// <summary>Agent decisions with one choice in the sample group.</summary>
    public static MergeDecisions AgentChoice(FieldChoice choice) => new([new GroupDecision(GroupKey, [choice])], MatchOverrides.None);

    /// <summary>Agent decisions that accept one finding.</summary>
    public static MergeDecisions AgentFinding(string originKey, string findingId) =>
        MergeDecisions.None with { Findings = [new ItemFindingDecisions(originKey, [new FindingChoice(findingId, FindingDecision.Accepted)])] };
}
