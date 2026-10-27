using PandaBear.VaultManager.Tool.Brief;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Brief;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests merging with agent decisions.</summary>
public sealed class MergeAgentDecisionsCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const string FilePassword = "fixture file password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-agent-").FullName;

    /// <summary>Deletes the temporary directory.</summary>
    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Merge_takes_the_agent_decisions_from_the_brief_and_writes_the_vault()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string briefPath = Path.Combine(directory, "brief.json");
        string agentPath = Path.Combine(directory, "agent.json");
        string[] briefArgs = ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--brief", briefPath, "--plaintext"];

        // Act
        var briefRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), briefArgs, TestContext.Current.CancellationToken);
        var group = BriefFile.Read(briefPath).Groups.ShouldHaveSingleItem();
        DecisionsFile.Write(agentPath, new MergeDecisions([new GroupDecision(group.Key, [FieldChoice.Member(FieldPaths.LoginPassword, CliInputs.BitwardenKey)])], MatchOverrides.None));
        var agentRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), [.. briefArgs, "--agent-decisions", agentPath], TestContext.Current.CancellationToken);

        // Assert
        briefRun.ExitCode.ShouldBe(2);
        agentRun.ExitCode.ShouldBe(0);
        agentRun.Error.ShouldBeEmpty();
        var item = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "merged.json").Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(CliInputs.BitwardenPassword);
        item.PasswordHistory.ShouldHaveSingleItem().Password.ShouldBe(CliInputs.KeeperPassword);
        DecisionsFile.Read(decisionsPath).Groups.ShouldHaveSingleItem().Choices.ShouldHaveSingleItem().Kind.ShouldBe(FieldChoiceKind.Pending);
    }

    [Fact]
    public async Task The_user_decision_wins_over_the_agent_decision_on_the_same_field()
    {
        // Arrange
        var bitwardenLogin = PipelineInputs.BitwardenLogin("bw-1", "Example", "alice", CliInputs.BitwardenPassword, "https://example.com", revised: false);
        bitwardenLogin["notes"] = "fixture bitwarden notes";
        string[] inputs =
        [
            PipelineInputs.Write(directory, "keeper.json", PipelineInputs.KeeperJson(PipelineInputs.KeeperLogin("rec-1", "Example", "alice", CliInputs.KeeperPassword, "https://example.com", "fixture keeper notes", modified: false))),
            PipelineInputs.Write(directory, "bitwarden.json", PipelineInputs.BitwardenJson(bitwardenLogin)),
        ];
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string briefPath = Path.Combine(directory, "brief.json");
        string agentPath = Path.Combine(directory, "agent.json");
        string groupKey = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Run(new PipelineRequest(inputs)).Unresolved[0].GroupKey;
        DecisionsFile.Write(decisionsPath, new MergeDecisions([new GroupDecision(groupKey, [FieldChoice.Member(FieldPaths.LoginPassword, CliInputs.KeeperKey)])], MatchOverrides.None));
        string[] args = ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--brief", briefPath, "--plaintext"];
        var briefRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);
        var password = BriefFile.Read(briefPath).Groups.ShouldHaveSingleItem().Fields.ShouldHaveSingleItem(field => field.FieldPath == FieldPaths.LoginPassword);
        DecisionsFile.Write(agentPath, new MergeDecisions(
            [new GroupDecision(groupKey, [FieldChoice.Member(FieldPaths.LoginPassword, CliInputs.BitwardenKey), FieldChoice.Member(FieldPaths.Notes, CliInputs.BitwardenKey)])],
            MatchOverrides.None));

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), [.. args, "--agent-decisions", agentPath], TestContext.Current.CancellationToken);

        // Assert
        briefRun.ExitCode.ShouldBe(2);
        password.NeedsDecision.ShouldBeFalse();
        password.AgentMayDecide.ShouldBeTrue();
        run.ExitCode.ShouldBe(0);
        var item = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "merged.json").Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(CliInputs.KeeperPassword);
        item.Notes.ShouldBe("fixture bitwarden notes");
    }

    [Theory]
    [InlineData("empty choice", "is Empty")]
    [InlineData("link", "links are not allowed")]
    [InlineData("split", "splits are not allowed")]
    [InlineData("notes", "notes decisions are not allowed")]
    [InlineData("unknown group", "is not in the brief")]
    [InlineData("list field", "is not one that the brief lets an agent decide")]
    [InlineData("member without a value", "which holds no value for it in the brief")]
    [InlineData("unknown finding", "is not in the brief")]
    public async Task Merge_refuses_agent_decisions_outside_the_limits_before_any_password_prompt(string change, string problem)
    {
        // Arrange
        string keeper = PipelineInputs.Write(directory, "keeper.json", PipelineInputs.KeeperJson(PipelineInputs.KeeperLogin("rec-1", "Example", "alice", CliInputs.KeeperPassword, "https://example.com", modified: false)));
        string bitwarden = CliInputs.ProtectedBitwarden(directory, FilePassword);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string briefPath = Path.Combine(directory, "brief.json");
        string agentPath = Path.Combine(directory, "agent.json");
        BriefFile.Write(briefPath, BriefSamples.Document(), TestContext.Current.CancellationToken);
        DecisionsFile.Write(agentPath, change switch
        {
            "empty choice" => BriefSamples.AgentChoice(FieldChoice.Empty(FieldPaths.LoginPassword)),
            "link" => MergeDecisions.None with { Overrides = new MatchOverrides([new OriginKeyPair(BriefSamples.First, BriefSamples.Second)], []) },
            "split" => MergeDecisions.None with { Overrides = new MatchOverrides([], [new OriginKeyPair(BriefSamples.First, BriefSamples.Second)]) },
            "notes" => MergeDecisions.None with { Notes = [new ItemNotesDecision(BriefSamples.First, NotesDecision.Share)] },
            "unknown group" => new MergeDecisions([new GroupDecision("0000000000000000", [FieldChoice.Member(FieldPaths.LoginPassword, BriefSamples.First)])], MatchOverrides.None),
            "list field" => BriefSamples.AgentChoice(FieldChoice.Member(FieldPaths.LoginUris, BriefSamples.First)),
            "member without a value" => BriefSamples.AgentChoice(FieldChoice.Member(FieldPaths.LoginTotp, BriefSamples.Second)),
            _ => BriefSamples.AgentFinding(BriefSamples.First, "notes-pin@notes:4"),
        });
        var prompts = new TestPrompts(ExportPassword, FilePassword);

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", keeper, bitwarden, "--output", output, "--decisions", decisionsPath, "--brief", briefPath, "--agent-decisions", agentPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("agent.json: ", StringComparison.Ordinal);
        run.Error.ShouldContain(problem, StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
        File.Exists(decisionsPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Merge_checks_the_agent_decisions_against_the_inputs_as_read_so_an_edited_brief_adds_no_field()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string briefPath = Path.Combine(directory, "brief.json");
        string agentPath = Path.Combine(directory, "agent.json");
        string[] args = ["merge", .. inputs, "--output", output, "--brief", briefPath, "--plaintext"];
        (await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken)).ExitCode.ShouldBe(2);
        var brief = BriefFile.Read(briefPath);
        var group = brief.Groups.ShouldHaveSingleItem();
        var edited = brief with
        {
            Groups = [group with { Fields = [.. group.Fields, new BriefField(FieldPaths.LoginUsername, NeedsDecision: false, AgentMayDecide: true, [new BriefHolder(CliInputs.KeeperKey, Newest: false)])] }]
        };
        BriefFile.Write(briefPath, edited, TestContext.Current.CancellationToken);
        DecisionsFile.Write(agentPath, new MergeDecisions([new GroupDecision(group.Key, [FieldChoice.Member(FieldPaths.LoginUsername, CliInputs.KeeperKey)])], MatchOverrides.None));
        var prompts = new TestPrompts(ExportPassword);

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), [.. args, "--agent-decisions", agentPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("field login.username of group " + group.Key + " is not one that the brief lets an agent decide", StringComparison.Ordinal);
        run.Error.ShouldContain("no longer fits these inputs and decisions", StringComparison.Ordinal);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Merge_refuses_a_brief_of_other_input_files_before_any_password_prompt()
    {
        // Arrange
        string keeper = PipelineInputs.Write(directory, "keeper.json", PipelineInputs.KeeperJson(PipelineInputs.KeeperLogin("rec-1", "Example", "alice", CliInputs.KeeperPassword, "https://example.com", modified: false)));
        string bitwarden = CliInputs.ProtectedBitwarden(directory, FilePassword);
        string output = Path.Combine(directory, "merged.json");
        string briefPath = Path.Combine(directory, "brief.json");
        string agentPath = PipelineInputs.Write(directory, "agent.json", """{"version":2,"groups":[],"links":[],"splits":[]}""");
        BriefFile.Write(briefPath, BriefSamples.Document(), TestContext.Current.CancellationToken);
        var prompts = new TestPrompts(ExportPassword, FilePassword);

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", keeper, bitwarden, "--output", output, "--brief", briefPath, "--agent-decisions", agentPath, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain(briefPath + " was made from keeper.json, bitwarden.json, not from keeper.json, protected.json.", StringComparison.Ordinal);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task The_user_finishes_what_the_agent_left_and_the_same_agent_file_still_applies()
    {
        // Arrange
        var bitwardenLogin = PipelineInputs.BitwardenLogin("bw-1", "Example", "alice", CliInputs.BitwardenPassword, "https://example.com", revised: false);
        bitwardenLogin["notes"] = "fixture bitwarden notes";
        string[] inputs =
        [
            PipelineInputs.Write(directory, "keeper.json", PipelineInputs.KeeperJson(PipelineInputs.KeeperLogin("rec-1", "Example", "alice", CliInputs.KeeperPassword, "https://example.com", "fixture keeper notes", modified: false))),
            PipelineInputs.Write(directory, "bitwarden.json", PipelineInputs.BitwardenJson(bitwardenLogin)),
        ];
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string briefPath = Path.Combine(directory, "brief.json");
        string agentPath = Path.Combine(directory, "agent.json");
        string[] args = ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--brief", briefPath, "--plaintext"];
        (await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken)).ExitCode.ShouldBe(2);
        string groupKey = BriefFile.Read(briefPath).Groups.ShouldHaveSingleItem().Key;
        DecisionsFile.Write(agentPath, new MergeDecisions([new GroupDecision(groupKey, [FieldChoice.Member(FieldPaths.LoginPassword, CliInputs.BitwardenKey)])], MatchOverrides.None));

        // Act
        var partial = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), [.. args, "--agent-decisions", agentPath], TestContext.Current.CancellationToken);
        var pendingChoice = DecisionsFile.Read(decisionsPath).Groups.ShouldHaveSingleItem().Choices.ShouldHaveSingleItem();
        DecisionsFile.Write(decisionsPath, new MergeDecisions([new GroupDecision(groupKey, [FieldChoice.Member(FieldPaths.Notes, CliInputs.KeeperKey)])], MatchOverrides.None));
        var finished = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), [.. args, "--agent-decisions", agentPath], TestContext.Current.CancellationToken);

        // Assert
        partial.ExitCode.ShouldBe(2);
        partial.Output.ShouldContain("Agent decisions applied: 1\n", StringComparison.Ordinal);
        pendingChoice.FieldPath.ShouldBe(FieldPaths.Notes);
        finished.ExitCode.ShouldBe(0);
        var item = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "merged.json").Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(CliInputs.BitwardenPassword);
        item.Notes.ShouldBe("fixture keeper notes");
    }

    [Fact]
    public async Task Merge_lists_the_pre_accepted_findings_that_the_agent_rejects()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(1300);
        string input = CliInputs.CodesAndPasswordInNotes(directory, codes);
        string output = Path.Combine(directory, "merged.json");
        string briefPath = Path.Combine(directory, "brief.json");
        string agentPath = Path.Combine(directory, "agent.json");
        string report = Path.Combine(directory, "report.md");
        string[] args = ["merge", input, "--output", output, "--brief", briefPath, "--plaintext"];
        (await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken)).ExitCode.ShouldBe(2);
        var member = BriefFile.Read(briefPath).Groups.ShouldHaveSingleItem().Members.ShouldHaveSingleItem();
        string codesId = member.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.RecoveryCodes).Id;
        string passwordId = member.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password).Id;
        DecisionsFile.Write(agentPath, MergeDecisions.None with
        {
            Findings = [new ItemFindingDecisions(member.OriginKey, [new FindingChoice(codesId, FindingDecision.Rejected), new FindingChoice(passwordId, FindingDecision.Accepted)])],
        });

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), [.. args, "--agent-decisions", agentPath, "--report", report], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("  Rejected by agent: 1\n", StringComparison.Ordinal);
        run.Output.ShouldContain("Agent decisions applied: 2\n", StringComparison.Ordinal);
        run.Output.ShouldContain($"Rejected by agent: {codesId} of {CliInputs.GitHubKey} (GitHub)\n", StringComparison.Ordinal);
        string markdown = await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken);
        markdown.ShouldContain("## Rejected by agent", StringComparison.Ordinal);
        markdown.ShouldContain($"| `keeper\\|keeper.json\\|rec-github` | `{passwordId}` | accepted |", StringComparison.Ordinal);
        markdown.ShouldContain($"| GitHub | `keeper\\|keeper.json\\|rec-github` | `{codesId}` |", StringComparison.Ordinal);
        var item = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "merged.json").Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(CliInputs.NotesPassword);
        item.Notes.ShouldNotBeNull().ShouldContain(codes[0], StringComparison.Ordinal);
        item.Fields.ShouldNotContain(field => field.Type == CustomFieldType.Hidden);
        codes.ShouldAllSatisfy(code => run.Output.ShouldNotContain(code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Merge_needs_the_brief_the_agent_read()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string agentPath = PipelineInputs.Write(directory, "agent.json", """{"version":2,"groups":[],"links":[],"splits":[]}""");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", Path.Combine(directory, "merged.json"), "--agent-decisions", agentPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("--agent-decisions needs --brief", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Merge_refuses_an_agent_decisions_file_that_is_the_decisions_file()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string decisionsPath = PipelineInputs.Write(directory, "decisions.json", """{"version":2,"groups":[],"links":[],"splits":[]}""");
        string briefPath = Path.Combine(directory, "brief.json");
        BriefFile.Write(briefPath, BriefSamples.Document(), TestContext.Current.CancellationToken);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", Path.Combine(directory, "merged.json"), "--decisions", decisionsPath, "--brief", briefPath, "--agent-decisions", decisionsPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("may be the same file as the agent decisions file", StringComparison.Ordinal);
    }
}
