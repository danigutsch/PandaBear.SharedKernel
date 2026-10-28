using PandaBear.Testing;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Tests merging with a decisions file.</summary>
public sealed class MergeDecisionsCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly TemporaryDirectory temporaryDirectory = new();

    private string DirectoryPath => temporaryDirectory.Path;

    /// <inheritdoc />
    public void Dispose() => temporaryDirectory.Dispose();

    [Fact]
    public async Task Merge_without_a_decisions_file_exits_2_and_asks_for_one()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(DirectoryPath);
        string output = Path.Combine(DirectoryPath, "merged.json");
        var prompts = new TestPrompts(ExportPassword);

        // Act
        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Output.ShouldContain("Fields that need a decision: 1", StringComparison.Ordinal);
        run.Error.ShouldContain("1 field(s) and 0 finding(s) need a decision.", StringComparison.Ordinal);
        run.Error.ShouldContain("--decisions <file>", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        Directory.GetFiles(DirectoryPath).Length.ShouldBe(2);
    }

    [Fact]
    public async Task Pending_field_entries_list_the_members_and_a_member_choice_resolves_them()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(DirectoryPath);
        string output = Path.Combine(DirectoryPath, "merged.json");
        string decisionsPath = Path.Combine(DirectoryPath, "decisions.json");
        string[] args = ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--plaintext"];

        // Act
        var pendingRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);
        bool writtenWhilePending = File.Exists(output);
        var pending = DecisionsFile.Read(decisionsPath);
        var group = pending.Groups.ShouldHaveSingleItem();
        DecisionsFile.Write(decisionsPath, pending with
        {
            Groups = [group with { Choices = [FieldChoice.Member(FieldPaths.LoginPassword, CliInputs.BitwardenKey)] }]
        });
        var decidedRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);

        // Assert
        pendingRun.ExitCode.ShouldBe(2);
        pendingRun.Error.ShouldContain("Pending entries are in " + decisionsPath, StringComparison.Ordinal);
        writtenWhilePending.ShouldBeFalse();
        var choice = group.Choices.ShouldHaveSingleItem();
        choice.FieldPath.ShouldBe(FieldPaths.LoginPassword);
        choice.Kind.ShouldBe(FieldChoiceKind.Pending);
        choice.Options.ShouldNotBeNull().ShouldBe([new FieldChoiceOption(CliInputs.KeeperKey, "Example"), new FieldChoiceOption(CliInputs.BitwardenKey, "Example")]);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(decisionsPath).ShouldBe(OwnerOnly);
        }

        decidedRun.ExitCode.ShouldBe(0);
        decidedRun.Error.ShouldBeEmpty();
        var item = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "merged.json").Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(CliInputs.BitwardenPassword);
        item.PasswordHistory.ShouldHaveSingleItem().Password.ShouldBe(CliInputs.KeeperPassword);
        DecisionsFile.Read(decisionsPath).Groups.ShouldHaveSingleItem().Choices.ShouldBe([FieldChoice.Member(FieldPaths.LoginPassword, CliInputs.BitwardenKey)]);
    }

    [Fact]
    public async Task Pending_finding_entries_name_proposed_findings_only_and_accepting_them_applies_them()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(1000);
        string input = CliInputs.CodesAndPasswordInNotes(DirectoryPath, codes);
        string output = Path.Combine(DirectoryPath, "merged.json");
        string decisionsPath = Path.Combine(DirectoryPath, "decisions.json");
        string[] args = ["merge", input, "--output", output, "--decisions", decisionsPath, "--plaintext"];

        // Act
        var pendingRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);
        bool writtenWhilePending = File.Exists(output);
        var pending = DecisionsFile.Read(decisionsPath);
        var item = pending.Findings.ShouldHaveSingleItem();
        var choice = item.Choices.ShouldHaveSingleItem();
        DecisionsFile.Write(decisionsPath, pending with
        {
            Findings = [item with { Choices = [choice with { Decision = FindingDecision.Accepted }] }]
        });
        var decidedRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);

        // Assert
        pendingRun.ExitCode.ShouldBe(2);
        pendingRun.Error.ShouldContain("0 field(s) and 1 finding(s) need a decision.", StringComparison.Ordinal);
        writtenWhilePending.ShouldBeFalse();
        item.OriginKey.ShouldBe(CliInputs.GitHubKey);
        item.ItemName.ShouldBe("GitHub");
        choice.Decision.ShouldBe(FindingDecision.Pending);
        choice.FindingId.ShouldContain("@notes:", StringComparison.Ordinal);
        decidedRun.ExitCode.ShouldBe(0);
        var merged = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "merged.json").Items.ShouldHaveSingleItem();
        merged.Login.ShouldNotBeNull().Password.ShouldBe(CliInputs.NotesPassword);
        merged.Fields.ShouldHaveSingleItem().Value.ShouldBe(string.Join('\n', codes));
    }

    [Fact]
    public async Task Updating_the_decisions_file_keeps_its_decisions_and_overrides()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(1010);
        string input = CliInputs.CodesAndPasswordInNotes(DirectoryPath, codes);
        string output = Path.Combine(DirectoryPath, "merged.json");
        string decisionsPath = Path.Combine(DirectoryPath, "decisions.json");
        var findings = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Run(new PipelineRequest([input])).Findings.ShouldHaveSingleItem();
        string codesId = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.RecoveryCodes).Id;
        string passwordId = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password).Id;
        var split = new OriginKeyPair(CliInputs.GitHubKey, "keeper|keeper.json|rec-github/" + codesId);
        DecisionsFile.Write(decisionsPath, new MergeDecisions([], new MatchOverrides([], [split]))
        {
            Findings = [new ItemFindingDecisions(CliInputs.GitHubKey, [new FindingChoice(codesId, FindingDecision.Rejected)])],
        });

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", input, "--output", output, "--decisions", decisionsPath, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(2);
        var updated = DecisionsFile.Read(decisionsPath);
        updated.Overrides.Splits.ShouldBe([split]);
        updated.Findings.ShouldHaveSingleItem().Choices.ShouldBe([new FindingChoice(codesId, FindingDecision.Rejected), new FindingChoice(passwordId, FindingDecision.Pending)]);
    }

    [Fact]
    public async Task Merge_warns_about_decisions_that_match_nothing_so_a_typo_is_not_ignored()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(1020);
        string[] inputs = CliInputs.CodesInKeeperNotes(DirectoryPath, codes);
        string output = Path.Combine(DirectoryPath, "merged.json");
        string decisionsPath = Path.Combine(DirectoryPath, "decisions.json");
        var planned = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Run(new PipelineRequest(inputs));
        var group = planned.Match.Groups.ShouldHaveSingleItem(candidate => candidate.Members.Count == 2);
        string codesId = planned.Findings.ShouldHaveSingleItem().Findings.ShouldHaveSingleItem().Id;
        var decisions = new MergeDecisions(
            [
                new GroupDecision("0000000000000000", [FieldChoice.Empty(FieldPaths.Notes)]),
                new GroupDecision(group.Key, [FieldChoice.Empty("login.pasword"), FieldChoice.Member(FieldPaths.Name, "keeper|keeper.json|rec-gone"), FieldChoice.Empty("card.number")]),
            ],
            new MatchOverrides([new OriginKeyPair(CliInputs.GitHubKey, "bitwarden|bitwarden.json|bw-gone")], []))
        {
            Findings =
            [
                new ItemFindingDecisions(CliInputs.GitHubKey, [new FindingChoice(codesId + "0", FindingDecision.Rejected)]),
                new ItemFindingDecisions("keeper|keeper.json|rec-gone", [new FindingChoice(codesId, FindingDecision.Rejected)]),
            ],
        };
        DecisionsFile.Write(decisionsPath, decisions);
        string before = await File.ReadAllTextAsync(decisionsPath, TestContext.Current.CancellationToken);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        string[] warnings =
        [
            "Warning: decisions for group 0000000000000000 match no merged group and are ignored.",
            "Warning: the decision on field login.pasword of group " + group.Key + " matches no field that takes Empty decisions and is ignored.",
            "Warning: the decision on field name of group " + group.Key + " names keeper|keeper.json|rec-gone, which is not in the group, and is ignored. Members: " + string.Join(", ", group.MemberKeys) + ".",
            "Warning: the decision on field card.number of group " + group.Key + " matches no field that takes Empty decisions and is ignored.",
            "Warning: the link between " + CliInputs.GitHubKey + " and bitwarden|bitwarden.json|bw-gone names bitwarden|bitwarden.json|bw-gone, which matches no item, and is ignored.",
            "Warning: the decision on finding " + codesId + "0 of " + CliInputs.GitHubKey + " matches no finding of the item and is ignored.",
            "Warning: finding decisions for keeper|keeper.json|rec-gone match no item with findings and are ignored.",
        ];
        warnings.ShouldAllSatisfy(warning => run.Error.ShouldContain(warning + "\n", StringComparison.Ordinal));
        string after = await File.ReadAllTextAsync(decisionsPath, TestContext.Current.CancellationToken);
        after.ShouldBe(before);
        var merged = BitwardenJsonReader.Read(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken), "merged.json").Items.ShouldHaveSingleItem();
        merged.Fields.ShouldHaveSingleItem(field => field.Type == CustomFieldType.Hidden).Value.ShouldBe(string.Join('\n', codes));
    }

    [Fact]
    public async Task Merge_rejects_a_decisions_file_with_a_misspelled_section_instead_of_applying_findings_it_rejects()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(1030);
        string[] inputs = CliInputs.CodesInKeeperNotes(DirectoryPath, codes);
        string output = Path.Combine(DirectoryPath, "merged.json");
        var planned = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Run(new PipelineRequest(inputs));
        string codesId = planned.Findings.ShouldHaveSingleItem().Findings.ShouldHaveSingleItem().Id;
        string json = $$"""{"version":2,"groups":[],"links":[],"splits":[],"finding":[{"originKey":"{{CliInputs.GitHubKey}}","choices":[{"findingId":"{{codesId}}","decision":"Rejected"}]}]}""";
        string decisionsPath = PipelineInputs.Write(DirectoryPath, "decisions.json", json);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain(decisionsPath, StringComparison.Ordinal);
        run.Error.ShouldContain("$.finding", StringComparison.Ordinal);
        run.Error.ShouldNotContain(codesId, StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        string after = await File.ReadAllTextAsync(decisionsPath, TestContext.Current.CancellationToken);
        after.ShouldBe(json);
    }

    [Fact]
    public async Task Merge_rejects_a_decisions_file_with_a_repeated_section_instead_of_applying_findings_it_rejects()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(1040);
        string[] inputs = CliInputs.CodesInKeeperNotes(DirectoryPath, codes);
        string output = Path.Combine(DirectoryPath, "merged.json");
        var planned = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Run(new PipelineRequest(inputs));
        string codesId = planned.Findings
            .ShouldHaveSingleItem(item => string.Equals(item.OriginKey, CliInputs.GitHubKey, StringComparison.Ordinal)).Findings
            .ShouldHaveSingleItem(finding => finding.Kind == SecretKind.RecoveryCodes).Id;

        // A rejection added by hand above the empty findings section that a merge writes.
        string json = $$"""
            {
              "version": 2,
              "findings": [{ "originKey": "{{CliInputs.GitHubKey}}", "choices": [{ "findingId": "{{codesId}}", "decision": "Rejected" }] }],
              "groups": [],
              "links": [],
              "splits": [],
              "findings": []
            }
            """;
        string decisionsPath = PipelineInputs.Write(DirectoryPath, "decisions.json", json);

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Output.ShouldBeEmpty();
        run.Error.ShouldContain(decisionsPath, StringComparison.Ordinal);
        run.Error.ShouldContain(" $.findings ", StringComparison.Ordinal);
        run.Error.ShouldContain("repeated property", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        string after = await File.ReadAllTextAsync(decisionsPath, TestContext.Current.CancellationToken);
        after.ShouldBe(json);
    }

    [Fact]
    public async Task Member_decision_naming_an_item_outside_its_group_is_warned_about_and_replaced_by_a_pending_entry()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(DirectoryPath);
        string output = Path.Combine(DirectoryPath, "merged.json");
        string decisionsPath = Path.Combine(DirectoryPath, "decisions.json");
        string[] args = ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--plaintext"];
        var conflict = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Run(new PipelineRequest(inputs)).Unresolved.ShouldHaveSingleItem();
        DecisionsFile.Write(decisionsPath, new MergeDecisions([new GroupDecision(conflict.GroupKey, [FieldChoice.Member(conflict.FieldPath, "keeper|keeper.json|rec-gone")])], MatchOverrides.None));

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("names keeper|keeper.json|rec-gone, which is not in the group, and is ignored.", StringComparison.Ordinal);
        run.Error.ShouldContain("Pending entries are in " + decisionsPath, StringComparison.Ordinal);
        var choice = DecisionsFile.Read(decisionsPath).Groups.ShouldHaveSingleItem().Choices.ShouldHaveSingleItem();
        choice.FieldPath.ShouldBe(conflict.FieldPath);
        choice.Kind.ShouldBe(FieldChoiceKind.Pending);
        choice.Options.ShouldNotBeNull().Select(option => option.OriginKey).ShouldBe(conflict.MemberOriginKeys);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Merge_warnings_replace_control_characters_from_the_decisions_file()
    {
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(DirectoryPath);
        string output = Path.Combine(DirectoryPath, "merged.json");
        string groupKey = string.Concat("0000", (char)0x1B, "[2J", (char)0x0D, (char)0x9B, "0000");
        string decisionsPath = Path.Combine(DirectoryPath, "decisions.json");
        DecisionsFile.Write(decisionsPath, new MergeDecisions([new GroupDecision(groupKey, [FieldChoice.Empty(FieldPaths.Notes)])], MatchOverrides.None));

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--plaintext"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Error.ShouldBe("Warning: decisions for group 0000\uFFFD[2J \uFFFD0000 match no merged group and are ignored.\n", StringComparer.Ordinal);
    }

    [Fact]
    public async Task Merge_with_the_ask_policy_leaves_every_conflict_to_the_decisions_file()
    {
        // Arrange
        string[] inputs = CliInputs.NewerBitwardenLogin(DirectoryPath);
        string output = Path.Combine(DirectoryPath, "merged.json");
        string decisionsPath = Path.Combine(DirectoryPath, "decisions.json");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--policy", "ask"], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(2);
        DecisionsFile.Read(decisionsPath).Groups.ShouldHaveSingleItem().Choices.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.LoginPassword);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Merge_reports_an_invalid_decisions_file_and_leaves_it_unchanged()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(DirectoryPath);
        string output = Path.Combine(DirectoryPath, "merged.json");
        string decisionsPath = PipelineInputs.Write(DirectoryPath, "decisions.json", """{"version":1,"groups":[],"links":[],"splits":[]}""");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("only version 2 is supported", StringComparison.Ordinal);
        string after = await File.ReadAllTextAsync(decisionsPath, TestContext.Current.CancellationToken);
        after.ShouldBe("""{"version":1,"groups":[],"links":[],"splits":[]}""");
    }

    [Fact]
    public async Task Merge_report_lists_the_decisions_needed()
    {
        // Arrange
        string[] inputs = CliInputs.ConflictingLogins(DirectoryPath);
        string output = Path.Combine(DirectoryPath, "merged.json");
        string report = Path.Combine(DirectoryPath, "report.md");

        // Act
        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--report", report], TestContext.Current.CancellationToken);

        // Assert
        run.ExitCode.ShouldBe(2);
        string markdown = await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken);
        markdown.ShouldContain("The merged vault was not written because 1 field(s) need a decision.", StringComparison.Ordinal);
        markdown.ShouldContain("## Decisions needed", StringComparison.Ordinal);
        markdown.ShouldContain("| `login.password` | `keeper\\|keeper.json\\|rec-1` (Example), `bitwarden\\|bitwarden.json\\|bw-1` (Example) |", StringComparison.Ordinal);
    }
}
