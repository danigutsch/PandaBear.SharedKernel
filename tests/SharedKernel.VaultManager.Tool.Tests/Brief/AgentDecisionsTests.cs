using PandaBear.VaultManager.Tool.Brief;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Merge;
using static PandaBear.VaultManager.Tool.Tests.Brief.BriefSamples;

namespace PandaBear.VaultManager.Tool.Tests.Brief;

public sealed class AgentDecisionsTests
{
    [Fact]
    public void An_agent_may_take_a_member_value_and_decide_the_findings_the_brief_lists()
    {
        var agent = new MergeDecisions([new GroupDecision(GroupKey, [FieldChoice.Member(FieldPaths.LoginPassword, Second), FieldChoice.Pending(FieldPaths.LoginTotp, [])])], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(First, [new FindingChoice(PreAccepted, FindingDecision.Rejected), new FindingChoice(Proposed, FindingDecision.Accepted)])],
        };

        AgentDecisions.Check(agent, Document()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("empty choice", "is Empty")]
    [InlineData("link", "links are not allowed")]
    [InlineData("split", "splits are not allowed")]
    [InlineData("notes", "notes decisions are not allowed")]
    [InlineData("unknown group", "group 0000000000000000 is not in the brief")]
    [InlineData("list field", "field login.uris of group " + GroupKey + " is not one that the brief lets an agent decide")]
    [InlineData("unlisted field", "field notes of group " + GroupKey + " is not one that the brief lets an agent decide")]
    [InlineData("member without a value", "names " + Second + ", which holds no value for it in the brief")]
    [InlineData("member outside the group", "names keeper|keeper.json|rec-9, which holds no value for it in the brief")]
    [InlineData("unknown item", "finding decisions for keeper|keeper.json|rec-9 name an item that is not in the brief")]
    [InlineData("unknown finding", "finding notes-pin@notes:4 of " + First + " is not in the brief")]
    [InlineData("forbidden accept", "finding " + Username + " of " + First + " may not be accepted by an agent")]
    [InlineData("forbidden reject", "finding " + PrivateKey + " of " + Second + " may not be rejected by an agent")]
    [InlineData("repeated field", "decides field login.password of group " + GroupKey + " more than once")]
    [InlineData("repeated finding", "decides finding " + Proposed + " of " + First + " more than once")]
    public void An_agent_may_not_decide_what_the_limits_forbid(string change, string problem)
    {
        MergeDecisions agent = change switch
        {
            "empty choice" => AgentChoice(FieldChoice.Empty(FieldPaths.LoginPassword)),
            "link" => MergeDecisions.None with { Overrides = new MatchOverrides([new OriginKeyPair(First, Second)], []) },
            "split" => MergeDecisions.None with { Overrides = new MatchOverrides([], [new OriginKeyPair(First, Second)]) },
            "notes" => MergeDecisions.None with { Notes = [new ItemNotesDecision(First, NotesDecision.Share)] },
            "unknown group" => new MergeDecisions([new GroupDecision("0000000000000000", [FieldChoice.Member(FieldPaths.LoginPassword, First)])], MatchOverrides.None),
            "list field" => AgentChoice(FieldChoice.Member(FieldPaths.LoginUris, First)),
            "unlisted field" => AgentChoice(FieldChoice.Member(FieldPaths.Notes, First)),
            "member without a value" => AgentChoice(FieldChoice.Member(FieldPaths.LoginTotp, Second)),
            "member outside the group" => AgentChoice(FieldChoice.Member(FieldPaths.LoginPassword, "keeper|keeper.json|rec-9")),
            "unknown item" => AgentFinding("keeper|keeper.json|rec-9", Proposed),
            "forbidden accept" => AgentFinding(First, Username),
            "forbidden reject" => MergeDecisions.None with { Findings = [new ItemFindingDecisions(Second, [new FindingChoice(PrivateKey, FindingDecision.Rejected)])] },
            "repeated field" => new MergeDecisions(
                [new GroupDecision(GroupKey, [FieldChoice.Member(FieldPaths.LoginPassword, First)]), new GroupDecision(GroupKey, [FieldChoice.Member(FieldPaths.LoginPassword, Second)])],
                MatchOverrides.None),
            "repeated finding" => MergeDecisions.None with
            {
                Findings = [new ItemFindingDecisions(First, [new FindingChoice(Proposed, FindingDecision.Accepted), new FindingChoice(Proposed, FindingDecision.Rejected)])],
            },
            _ => AgentFinding(First, "notes-pin@notes:4"),
        };

        AgentDecisions.Check(agent, Document()).ShouldHaveSingleItem().ShouldContain(problem, StringComparison.Ordinal);
    }

    [Fact]
    public void The_user_decision_wins_on_the_same_field_and_finding()
    {
        var user = new MergeDecisions([new GroupDecision(GroupKey, [FieldChoice.Member(FieldPaths.LoginPassword, First)])], new MatchOverrides([], [new OriginKeyPair(First, "keeper|keeper.json|rec-2")]))
        {
            Findings = [new ItemFindingDecisions(First, [new FindingChoice(Proposed, FindingDecision.Rejected)])],
            Notes = [new ItemNotesDecision(First, NotesDecision.Private)],
        };
        var agent = new MergeDecisions([new GroupDecision(GroupKey, [FieldChoice.Member(FieldPaths.LoginPassword, Second), FieldChoice.Member(FieldPaths.LoginTotp, First)])], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(First, [new FindingChoice(Proposed, FindingDecision.Accepted), new FindingChoice(PreAccepted, FindingDecision.Rejected)])],
        };

        var combined = AgentDecisions.Combine(user, agent);

        combined.Find(GroupKey, FieldPaths.LoginPassword).ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, First));
        combined.Find(GroupKey, FieldPaths.LoginTotp).ShouldBe(FieldChoice.Member(FieldPaths.LoginTotp, First));
        combined.FindFinding(First, Proposed).ShouldBe(FindingDecision.Rejected);
        combined.FindFinding(First, PreAccepted).ShouldBe(FindingDecision.Rejected);
        combined.Overrides.ShouldBe(user.Overrides);
        combined.Notes.ShouldBe(user.Notes);
    }

    [Fact]
    public void Pre_accepted_findings_the_agent_rejects_are_listed_unless_the_user_decided_them()
    {
        var preAccepted = FindingSamples.Finding(Confidence.High);
        var proposed = FindingSamples.At("notes") with
        {
            RuleId = "fixture-proposed"
        };
        var other = FindingSamples.At("fields[0].value") with
        {
            Confidence = Confidence.High
        };
        ItemFindingsResult[] findings =
        [
            new(First, "Example", [preAccepted, proposed], []),
            new(Second, "Example (Bitwarden)", [other], [other.Id]),
        ];
        var user = MergeDecisions.None with
        {
            Findings = [new ItemFindingDecisions(Second, [new FindingChoice(other.Id, FindingDecision.Accepted)])]
        };
        var agent = MergeDecisions.None with
        {
            Findings =
            [
                new ItemFindingDecisions(First, [new FindingChoice(preAccepted.Id, FindingDecision.Rejected), new FindingChoice(proposed.Id, FindingDecision.Rejected)]),
                new ItemFindingDecisions(Second, [new FindingChoice(other.Id, FindingDecision.Rejected)]),
            ],
        };

        var rejected = AgentDecisions.RejectedByAgent(user, agent, findings);

        var item = rejected.ShouldHaveSingleItem();
        item.OriginKey.ShouldBe(First);
        item.ItemName.ShouldBe("Example");
        item.Choices.ShouldBe([new FindingChoice(preAccepted.Id, FindingDecision.Rejected)]);
    }

    [Fact]
    public void Finding_decisions_that_change_which_items_match_or_empty_a_chosen_value_are_conflicts()
    {
        var keeper = AgentResults.Login("rec-1", "keeper", "alice", "fixture-keeper-password");
        var bitwarden = AgentResults.Login("bw-1", "bitwarden", "alice", "fixture-bitwarden-password");
        var other = AgentResults.Login("bw-2", "bitwarden", "bob", "fixture-other-password");
        var userOnly = AgentResults.Result(("g1", [keeper, bitwarden]), ("g2", [other]));
        var regrouped = AgentResults.Result(("g3", [keeper, bitwarden, other]));
        var emptied = AgentResults.Result(("g1", [keeper, bitwarden with { Login = bitwarden.Login! with { Password = null } }]), ("g2", [other]));
        var decisions = new MergeDecisions([new GroupDecision("g1", [FieldChoice.Member(FieldPaths.LoginPassword, AgentResults.Key(bitwarden))])], MatchOverrides.None);

        AgentDecisions.Conflicts(userOnly, userOnly, decisions).ShouldBeEmpty();
        AgentDecisions.Conflicts(userOnly, regrouped, decisions).ShouldNotBeEmpty();
        AgentDecisions.Conflicts(userOnly, emptied, decisions).ShouldHaveSingleItem().ShouldContain("login.password", StringComparison.Ordinal);
    }

    [Fact]
    public void Effective_agent_decisions_leave_out_what_the_user_decides()
    {
        var user = new MergeDecisions([new GroupDecision(GroupKey, [FieldChoice.Member(FieldPaths.LoginPassword, First)])], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(First, [new FindingChoice(Proposed, FindingDecision.Rejected)])],
        };
        var agent = new MergeDecisions([new GroupDecision(GroupKey, [FieldChoice.Member(FieldPaths.LoginPassword, Second), FieldChoice.Member(FieldPaths.LoginTotp, First), FieldChoice.Pending(FieldPaths.Name, [])])], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(First, [new FindingChoice(Proposed, FindingDecision.Accepted), new FindingChoice(PreAccepted, FindingDecision.Rejected)])],
        };

        var effective = AgentDecisions.Effective(user, agent);

        effective.Groups.ShouldHaveSingleItem().Choices.ShouldBe([FieldChoice.Member(FieldPaths.LoginTotp, First)]);
        effective.Findings.ShouldHaveSingleItem().Choices.ShouldBe([new FindingChoice(PreAccepted, FindingDecision.Rejected)]);
    }
}
