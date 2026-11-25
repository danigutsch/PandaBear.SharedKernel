using SharedKernel.Testing;
using SharedKernel.Vault.Brief;
using SharedKernel.Vault.Matching;
using SharedKernel.Vault.Merge;
using SharedKernel.Vault.Pipeline;
using SharedKernel.Vault.Tests.Review;

namespace SharedKernel.Vault.Tests.Brief;

/// <summary>Tests agent decision replay without a command line or an export.</summary>
public sealed class AgentMergePreviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Preview_applies_agent_decisions_after_user_decisions_without_writing(bool userDecides)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        string output = Path.Combine(directory.Path, "merged.json");
        CancellationToken ct = TestContext.Current.CancellationToken;
        var scanned = ReviewCheckpointFixtures.ConflictingLogins();
        var pipeline = ReviewCheckpointFixtures.CreatePipeline();
        var baseline = pipeline.Merge(scanned, new MergeRequest(), ct);
        var brief = BriefBuilder.Build(scanned.InputPaths, scanned, baseline, MergeDecisions.None);
        var group = brief.Groups.ShouldHaveSingleItem();
        string keeper = ReviewCheckpointFixtures.KeeperKey;
        string bitwarden = group.Members.ShouldHaveSingleItem(member => member.OriginKey != keeper).OriginKey;
        var agent = new MergeDecisions([new GroupDecision(group.Key, [FieldChoice.Member(FieldPaths.LoginPassword, keeper)])], MatchOverrides.None);
        var request = new MergeRequest
        {
            Decisions = userDecides
                ? new MergeDecisions([new GroupDecision(group.Key, [FieldChoice.Member(FieldPaths.LoginPassword, bitwarden)])], MatchOverrides.None)
                : MergeDecisions.None,
            Output = new PipelineOutput(output, Plaintext: true),
        };

        // Act
        var preview = AgentMergePreview.Evaluate(pipeline, scanned, request, brief, agent, ct);

        // Assert
        preview.Failure.ShouldBeNull();
        preview.Problems.ShouldBeEmpty();
        preview.Decisions.ShouldNotBeNull().Find(group.Key, FieldPaths.LoginPassword)
            .ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, userDecides ? bitwarden : keeper));
        preview.Result.ShouldNotBeNull().OutputWritten.ShouldBeFalse();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public void A_field_added_to_the_saved_brief_cannot_expand_agent_authority()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        var scanned = ReviewCheckpointFixtures.ConflictingLogins();
        var pipeline = ReviewCheckpointFixtures.CreatePipeline();
        var request = new MergeRequest();
        var baseline = pipeline.Merge(scanned, request, ct);
        var original = BriefBuilder.Build(scanned.InputPaths, scanned, baseline, MergeDecisions.None);
        var group = original.Groups.ShouldHaveSingleItem();
        string origin = ReviewCheckpointFixtures.KeeperKey;
        var addedField = new BriefField(FieldPaths.LoginUsername, NeedsDecision: false, AgentMayDecide: true, [new BriefHolder(origin, Newest: false)]);
        var edited = original with { Groups = [group with { Fields = [.. group.Fields, addedField] }] };
        var agent = new MergeDecisions([new GroupDecision(group.Key, [FieldChoice.Member(FieldPaths.LoginUsername, origin)])], MatchOverrides.None);

        // Act
        var originalProblems = AgentDecisions.Check(agent, edited);
        var preview = AgentMergePreview.Evaluate(pipeline, scanned, request, edited, agent, ct);

        // Assert
        originalProblems.ShouldBeEmpty();
        preview.Failure.ShouldBe(AgentMergeFailure.CurrentInputs);
        preview.Problems.ShouldHaveSingleItem().ShouldContain("field login.username", StringComparison.Ordinal);
        preview.Result.ShouldBeNull();
        preview.Decisions.ShouldBeNull();
    }

    [Theory]
    [InlineData(AgentMergeFailure.InputFiles)]
    [InlineData(AgentMergeFailure.SavedBrief)]
    public void A_host_must_supply_the_briefs_input_order_and_original_scope(AgentMergeFailure expected)
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        var scanned = ReviewCheckpointFixtures.ConflictingLogins();
        var pipeline = ReviewCheckpointFixtures.CreatePipeline();
        var request = new MergeRequest();
        var baseline = pipeline.Merge(scanned, request, ct);
        var original = BriefBuilder.Build(scanned.InputPaths, scanned, baseline, MergeDecisions.None);
        string group = original.Groups.ShouldHaveSingleItem().Key;
        var brief = expected == AgentMergeFailure.InputFiles
            ? original with { Inputs = [.. original.Inputs.Reverse()] }
            : original with { Groups = [] };
        var agent = new MergeDecisions(
            [new GroupDecision(group, [FieldChoice.Member(FieldPaths.LoginPassword, ReviewCheckpointFixtures.KeeperKey)])],
            MatchOverrides.None);

        // Act
        var preview = AgentMergePreview.Evaluate(pipeline, scanned, request, brief, agent, ct);

        // Assert
        preview.Failure.ShouldBe(expected);
        preview.Problems.ShouldNotBeEmpty();
        preview.Result.ShouldBeNull();
        preview.Decisions.ShouldBeNull();
    }
}
