using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class MergeDecisionsCommandTests : IDisposable
{
    private const string ExportPassword = "fixture export password";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-decisions-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Merge_without_a_decisions_file_exits_2_and_asks_for_one()
    {
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword);

        var run = await CommandLineRun.Invoke(prompts.Environment(), ["merge", .. inputs, "--output", output], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(2);
        run.Output.ShouldContain("Fields that need a decision: 1", StringComparison.Ordinal);
        run.Error.ShouldContain("1 field(s) and 0 finding(s) need a decision.", StringComparison.Ordinal);
        run.Error.ShouldContain("--decisions <file>", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
        prompts.NewPasswordPrompts.ShouldBeEmpty();
        Directory.GetFiles(directory).Length.ShouldBe(2);
    }

    [Fact]
    public async Task Pending_field_entries_list_the_members_and_a_member_choice_resolves_them()
    {
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string[] args = ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--plaintext"];

        var pendingRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);
        bool writtenWhilePending = File.Exists(output);
        var pending = DecisionsFile.Read(decisionsPath);
        var group = pending.Groups.ShouldHaveSingleItem();
        DecisionsFile.Write(decisionsPath, pending with { Groups = [group with { Choices = [FieldChoice.Member(FieldPaths.LoginPassword, CliInputs.BitwardenKey)] }] });
        var decidedRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);

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
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(1000);
        string input = CliInputs.CodesAndPasswordInNotes(directory, codes);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string[] args = ["merge", input, "--output", output, "--decisions", decisionsPath, "--plaintext"];

        var pendingRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);
        bool writtenWhilePending = File.Exists(output);
        var pending = DecisionsFile.Read(decisionsPath);
        var item = pending.Findings.ShouldHaveSingleItem();
        var choice = item.Choices.ShouldHaveSingleItem();
        DecisionsFile.Write(decisionsPath, pending with { Findings = [item with { Choices = [choice with { Decision = FindingDecision.Accepted }] }] });
        var decidedRun = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);

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
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(1010);
        string input = CliInputs.CodesAndPasswordInNotes(directory, codes);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        var findings = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Run(new PipelineRequest([input])).Findings.ShouldHaveSingleItem();
        string codesId = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.RecoveryCodes).Id;
        string passwordId = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password).Id;
        var split = new OriginKeyPair(CliInputs.GitHubKey, "keeper|keeper.json|rec-github/" + codesId);
        DecisionsFile.Write(decisionsPath, new MergeDecisions([], new MatchOverrides([], [split]))
        {
            Findings = [new ItemFindingDecisions(CliInputs.GitHubKey, [new FindingChoice(codesId, FindingDecision.Rejected)])],
        });

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", input, "--output", output, "--decisions", decisionsPath, "--plaintext"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(2);
        var updated = DecisionsFile.Read(decisionsPath);
        updated.Overrides.Splits.ShouldBe([split]);
        updated.Findings.ShouldHaveSingleItem().Choices.ShouldBe([new FindingChoice(codesId, FindingDecision.Rejected), new FindingChoice(passwordId, FindingDecision.Pending)]);
    }

    [Fact]
    public async Task Merge_warns_about_decisions_that_match_nothing_so_a_typo_is_not_ignored()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(1020);
        string[] inputs = CliInputs.CodesInKeeperNotes(directory, codes);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
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

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--plaintext"], TestContext.Current.CancellationToken);

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
    public async Task Merge_with_the_ask_policy_leaves_every_conflict_to_the_decisions_file()
    {
        string[] inputs = CliInputs.NewerBitwardenLogin(directory);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath, "--policy", "ask"], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(2);
        DecisionsFile.Read(decisionsPath).Groups.ShouldHaveSingleItem().Choices.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.LoginPassword);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public async Task Merge_reports_an_invalid_decisions_file_and_leaves_it_unchanged()
    {
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = PipelineInputs.Write(directory, "decisions.json", """{"version":1,"groups":[],"links":[],"splits":[]}""");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--decisions", decisionsPath], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(3);
        run.Error.ShouldContain("only version 2 is supported", StringComparison.Ordinal);
        string after = await File.ReadAllTextAsync(decisionsPath, TestContext.Current.CancellationToken);
        after.ShouldBe("""{"version":1,"groups":[],"links":[],"splits":[]}""");
    }

    [Fact]
    public async Task Merge_report_lists_the_decisions_needed()
    {
        string[] inputs = CliInputs.ConflictingLogins(directory);
        string output = Path.Combine(directory, "merged.json");
        string report = Path.Combine(directory, "report.md");

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["merge", .. inputs, "--output", output, "--report", report], TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(2);
        string markdown = await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken);
        markdown.ShouldContain("The merged vault was not written because 1 field(s) need a decision.", StringComparison.Ordinal);
        markdown.ShouldContain("## Decisions needed", StringComparison.Ordinal);
        markdown.ShouldContain("| `login.password` | `keeper\\|keeper.json\\|rec-1` (Example), `bitwarden\\|bitwarden.json\\|bw-1` (Example) |", StringComparison.Ordinal);
    }
}
