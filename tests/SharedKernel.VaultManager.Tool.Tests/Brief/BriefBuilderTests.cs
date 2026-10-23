using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Brief;
using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Brief;

public sealed class BriefBuilderTests : IDisposable
{
    private const string KeeperKey = "keeper|keeper.json|rec-1";
    private const string BitwardenKey = "bitwarden|bitwarden.json|bw-1";
    private const string KeeperPassword = "fixture-keeper-brief-password";
    private const string BitwardenPassword = "fixture-bitwarden-brief-password";
    private const string KeeperNotes = "Keeper notes about the account";
    private const string BitwardenNotes = "Bitwarden notes about the account";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-brief-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Brief_lists_only_the_groups_that_need_a_decision()
    {
        string[] inputs =
        [
            Write(directory, "keeper.json", KeeperJson(
                KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com", modified: false),
                KeeperLogin("rec-2", "Mail", "bob", "fixture-same-mail-password", "https://mail.example.com"))),
            Write(directory, "bitwarden.json", BitwardenJson(
                BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com", revised: false),
                BitwardenLogin("bw-2", "Mail", "bob", "fixture-same-mail-password", "https://mail.example.com"))),
        ];

        (BriefDocument brief, var result) = BriefRun.Build(inputs);

        brief.Version.ShouldBe(BriefFile.CurrentVersion);
        var group = brief.Groups.ShouldHaveSingleItem();
        group.Key.ShouldBe(result.Unresolved.ShouldHaveSingleItem().GroupKey);
        group.Type.ShouldBe(VaultItemType.Login);
        group.Members.Select(member => (member.OriginKey, member.SourceFile, member.Name, member.Host, member.Username, member.Modified, member.Newest)).ShouldBe(
        [
            (KeeperKey, "keeper.json", "Example", (string?)"example.com", (string?)"alice", (DateTimeOffset?)null, false),
            (BitwardenKey, "bitwarden.json", "Example", "example.com", "alice", null, false),
        ]);
        var password = group.Fields.ShouldHaveSingleItem();
        password.FieldPath.ShouldBe(FieldPaths.LoginPassword);
        password.NeedsDecision.ShouldBeTrue();
        password.AgentMayDecide.ShouldBeTrue();
        password.Holders.ShouldBe(
        [
            new BriefHolder(KeeperKey, Newest: false, Weak: false, Reused: false, InOtherHistory: false),
            new BriefHolder(BitwardenKey, Newest: false, Weak: false, Reused: false, InOtherHistory: false),
        ]);
        brief.Summary.ShouldBe(new BriefSummary(Items: 2, MergedGroups: 2, ConflictsResolvedByPolicy: 0, FieldsNeedingDecision: 1, Findings: 0, FindingsApplied: 0, FindingsNeedingDecision: 0, PasswordIssues: 0, AttachmentsToUpload: 0));
    }

    [Fact]
    public void Brief_shows_names_folders_safe_user_names_masked_addresses_and_shared_notes_only()
    {
        JsonObject keeper = KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://alice:fixture-uri-secret@example.com/reset?token=fixture-uri-token", KeeperNotes, modified: false);
        keeper["folders"] = new JsonArray(new JsonObject { ["folder"] = "Personal" });
        JsonObject bitwarden = BitwardenLogin("bw-1", "Example Bank", "alice.example", BitwardenPassword, "https://example.com/login", revised: false);
        bitwarden["notes"] = BitwardenNotes;
        bitwarden["folderId"] = "folder-work";
        var export = JsonNode.Parse(BitwardenJson(bitwarden)).ShouldNotBeNull();
        export["folders"] = new JsonArray(new JsonObject { ["id"] = "folder-work", ["name"] = "Work" });
        string[] inputs = [Write(directory, "keeper.json", KeeperJson(keeper)), Write(directory, "bitwarden.json", export.ToJsonString())];
        var decisions = BriefRun.Linked(KeeperKey, BitwardenKey) with { Notes = [new ItemNotesDecision(KeeperKey, NotesDecision.Share, NotesSha256: ItemNotesDecision.HashOf(KeeperNotes))] };

        var group = BriefRun.Build(inputs, decisions).Brief.Groups.ShouldHaveSingleItem();

        BriefRun.Field(group, FieldPaths.Name).Holders.Select(holder => holder.Value).ShouldBe(["Example", "Example Bank"]);
        var folder = BriefRun.Field(group, FieldPaths.Folder);
        folder.AgentMayDecide.ShouldBeTrue();
        folder.Holders.ShouldBe([new BriefHolder(KeeperKey, Newest: false, Value: "Personal"), new BriefHolder(BitwardenKey, Newest: false, Value: "Work")]);
        BriefRun.Field(group, FieldPaths.LoginUsername).Holders.Select(holder => holder.Value).ShouldBe(["alice", "alice.example"]);
        var uris = BriefRun.Field(group, FieldPaths.LoginUris);
        uris.AgentMayDecide.ShouldBeFalse();
        uris.Holders.Select(holder => holder.Values).ShouldBe([["https://***@example.com/reset?***"], ["https://example.com/login"]]);
        var notes = BriefRun.Field(group, FieldPaths.Notes);
        notes.NeedsDecision.ShouldBeTrue();
        notes.Holders.ShouldBe([new BriefHolder(KeeperKey, Newest: false, Value: KeeperNotes), new BriefHolder(BitwardenKey, Newest: false)]);
        group.Members[0].Host.ShouldBe("example.com");
        string json = BriefFile.Serialize(BriefRun.Build(inputs, decisions).Brief);
        string[] withheld = [KeeperPassword, BitwardenPassword, BitwardenNotes, "fixture-uri-secret", "fixture-uri-token"];
        withheld.ShouldAllSatisfy(value => json.ShouldNotContain(value, StringComparison.Ordinal));
    }

    [Fact]
    public void Brief_withholds_notes_shared_for_other_text_and_user_names_that_hold_a_finding()
    {
        string totp = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(3100));
        JsonObject keeper = KeeperLogin("rec-1", "Example", totp, KeeperPassword, "https://example.com", KeeperNotes, modified: false);
        JsonObject bitwarden = BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com", revised: false);
        bitwarden["notes"] = BitwardenNotes;
        string[] inputs = [Write(directory, "keeper.json", KeeperJson(keeper)), Write(directory, "bitwarden.json", BitwardenJson(bitwarden))];
        var stale = BriefRun.Linked(KeeperKey, BitwardenKey) with
        {
            Notes =
            [
                new ItemNotesDecision(KeeperKey, NotesDecision.Share, NotesSha256: ItemNotesDecision.HashOf("other notes")),
                new ItemNotesDecision(BitwardenKey, NotesDecision.Private),
            ],
        };

        (BriefDocument brief, _) = BriefRun.Build(inputs, stale, new MergePolicy(MergePolicyKind.Ask));

        var group = brief.Groups.ShouldHaveSingleItem();
        BriefRun.Field(group, FieldPaths.Notes).Holders.ShouldAllSatisfy(holder => holder.Value.ShouldBeNull());
        group.Members[0].Username.ShouldBeNull();
        BriefFile.Serialize(brief).ShouldNotContain(totp, StringComparison.Ordinal);
        BriefFile.Serialize(brief).ShouldNotContain(KeeperNotes, StringComparison.Ordinal);
    }

    [Fact]
    public void Brief_flags_passwords_without_showing_them()
    {
        JsonObject older = KeeperLogin("rec-1", "Router", "admin", "Older-Strong-Password-1", "http://192.168.1.1");
        JsonObject newer = BitwardenLogin("bw-1", "Router", "admin", "admin", "http://192.168.1.1");
        newer["passwordHistory"] = new JsonArray(new JsonObject { ["lastUsedDate"] = "2025-01-01T00:00:00.000Z", ["password"] = "Older-Strong-Password-1" });
        string[] inputs =
        [
            Write(directory, "keeper.json", KeeperJson(older, KeeperLogin("rec-2", "Shop", "carol", "admin", "https://shop.example.com"))),
            Write(directory, "bitwarden.json", BitwardenJson(newer)),
        ];

        var group = BriefRun.Build(inputs, policy: new MergePolicy(MergePolicyKind.Ask)).Brief.Groups.ShouldHaveSingleItem();

        BriefRun.Field(group, FieldPaths.LoginPassword).Holders.ShouldBe(
        [
            new BriefHolder(KeeperKey, Newest: false, Weak: false, Reused: false, InOtherHistory: true),
            new BriefHolder(BitwardenKey, Newest: true, Weak: true, Reused: true, InOtherHistory: false),
        ]);
        var history = BriefRun.Field(group, FieldPaths.PasswordHistory);
        history.AgentMayDecide.ShouldBeFalse();
        history.Holders.ShouldBe([new BriefHolder(BitwardenKey, Newest: false)]);
        group.Members.Select(member => member.Newest).ShouldBe([false, true]);
    }

    [Fact]
    public void Brief_lists_the_findings_of_a_single_item_whose_finding_needs_a_decision()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(3200);
        string input = Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: PipelineSecrets.RecoveryNotes(codes, "Gh-brief-Passw0rd!"))));

        (BriefDocument brief, var result) = BriefRun.Build([input]);

        var group = brief.Groups.ShouldHaveSingleItem();
        group.Fields.ShouldBeEmpty();
        var member = group.Members.ShouldHaveSingleItem();
        member.OriginKey.ShouldBe("keeper|keeper.json|rec-github");
        SecretFinding[] findings = [.. result.Findings.ShouldHaveSingleItem().Findings];
        member.Findings.Select(finding => finding.Id).ShouldBe(findings.Select(finding => finding.Id));
        var password = member.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        password.ShouldBe(new BriefFinding(password.Id, SecretKind.Password, Confidence.Medium, SecretLocation.Notes, 19, "login.password", PreAccepted: false, NeedsDecision: true, AgentMayAccept: true, AgentMayReject: true));
        var recovery = member.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.RecoveryCodes);
        recovery.PreAccepted.ShouldBeTrue();
        recovery.NeedsDecision.ShouldBeFalse();
        recovery.AgentMayReject.ShouldBeTrue();
        recovery.Line.ShouldBe(2);
        string json = BriefFile.Serialize(brief);
        codes.ShouldAllSatisfy(code => json.ShouldNotContain(code, StringComparison.Ordinal));
        json.ShouldNotContain("Gh-brief-Passw0rd!", StringComparison.Ordinal);
    }

    [Fact]
    public void Brief_withholds_shared_notes_that_hold_a_finding_whether_it_applies_or_not()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(3300);
        string keeperNotes = "Account notes\nPassword: Gh-brief-Notes-Passw0rd!";
        string bitwardenNotes = "Account notes\nGitHub recovery codes\n" + string.Join('\n', codes);
        JsonObject keeper = KeeperLogin("rec-1", "GitHub", "alice", KeeperPassword, "https://github.com", keeperNotes, modified: false);
        JsonObject bitwarden = BitwardenLogin("bw-1", "GitHub", "alice", BitwardenPassword, "https://github.com", revised: false);
        bitwarden["notes"] = bitwardenNotes;
        string[] inputs = [Write(directory, "keeper.json", KeeperJson(keeper)), Write(directory, "bitwarden.json", BitwardenJson(bitwarden))];
        var scan = BriefRun.Build(inputs).Result;
        string codesId = scan.Findings.ShouldHaveSingleItem(item => item.OriginKey == BitwardenKey).Findings.ShouldHaveSingleItem().Id;
        var decisions = MergeDecisions.None with
        {
            Findings = [new ItemFindingDecisions(BitwardenKey, [new FindingChoice(codesId, FindingDecision.Rejected)])],
            Notes =
            [
                new ItemNotesDecision(KeeperKey, NotesDecision.Share, NotesSha256: ItemNotesDecision.HashOf(keeperNotes)),
                new ItemNotesDecision(BitwardenKey, NotesDecision.Share, NotesSha256: ItemNotesDecision.HashOf(bitwardenNotes)),
            ],
        };

        (BriefDocument brief, _) = BriefRun.Build(inputs, decisions);

        var notes = BriefRun.Field(brief.Groups.ShouldHaveSingleItem(), FieldPaths.Notes);
        notes.Holders.ShouldBe([new BriefHolder(KeeperKey, Newest: false), new BriefHolder(BitwardenKey, Newest: false)]);
        string json = BriefFile.Serialize(brief);
        json.ShouldNotContain("Gh-brief-Notes-Passw0rd!", StringComparison.Ordinal);
        codes.ShouldAllSatisfy(code => json.ShouldNotContain(code, StringComparison.Ordinal));
    }

    [Fact]
    public void Brief_shows_user_names_and_addresses_only_as_the_inputs_hold_them()
    {
        const string Connection = "postgres://fixture-db-user:fixture-db-secret@db.internal.example:5432/app";
        JsonObject keeper = KeeperLogin("rec-1", "Database", null, KeeperPassword, "https://example.com", Connection, modified: false);
        JsonObject bitwarden = BitwardenLogin("bw-1", "Database", "alice", BitwardenPassword, "https://example.com", revised: false);
        string[] inputs = [Write(directory, "keeper.json", KeeperJson(keeper)), Write(directory, "bitwarden.json", BitwardenJson(bitwarden))];
        var connection = BriefRun.Build(inputs).Result.Findings.ShouldHaveSingleItem().Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.ConnectionString);
        var decisions = BriefRun.Linked(KeeperKey, BitwardenKey) with
        {
            Findings = [new ItemFindingDecisions(KeeperKey, [new FindingChoice(connection.Id, FindingDecision.Accepted)])],
        };

        (BriefDocument brief, var result) = BriefRun.Build(inputs, decisions);

        result.Match.Groups.ShouldHaveSingleItem().Members[0].Login.ShouldNotBeNull().Username.ShouldBe("fixture-db-user");
        var group = brief.Groups.ShouldHaveSingleItem();
        group.Members.Select(member => (member.Username, member.Host)).ShouldBe([((string?)null, (string?)"example.com"), ("alice", "example.com")]);
        BriefRun.Field(group, FieldPaths.LoginUsername).Holders.ShouldBe([new BriefHolder(KeeperKey, Newest: false), new BriefHolder(BitwardenKey, Newest: false, Value: "alice")]);
        BriefRun.Field(group, FieldPaths.LoginUris).Holders[0].Values.ShouldBe(["https://example.com"]);
        var connectionFinding = group.Members[0].Findings.ShouldHaveSingleItem(finding => finding.Id == connection.Id);
        connectionFinding.AgentMayAccept.ShouldBeFalse();
        connectionFinding.AgentMayReject.ShouldBe(!connection.IsPreAccepted);
        string json = BriefFile.Serialize(brief);
        string[] withheld = ["fixture-db-user", "fixture-db-secret", "db.internal.example"];
        withheld.ShouldAllSatisfy(value => json.ShouldNotContain(value, StringComparison.Ordinal));
    }

    [Fact]
    public void Brief_gives_no_host_for_an_address_whose_host_is_masked()
    {
        string[] inputs =
        [
            Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-1", "Router", "admin", KeeperPassword, "https://admin:fixture/router@router.local", modified: false))),
            Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Router", "admin", BitwardenPassword, "https://admin:fixture/router@router.local", revised: false))),
        ];

        var members = BriefRun.Build(inputs).Brief.Groups.ShouldHaveSingleItem().Members;

        members.ShouldAllSatisfy(member => member.Host.ShouldBeNull());
    }

    [Fact]
    public void Brief_records_the_input_files_it_was_made_from()
    {
        string[] inputs =
        [
            Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com", modified: false))),
            Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com", revised: false))),
        ];

        (BriefDocument brief, _) = BriefRun.Build(inputs);

        brief.Inputs.ShouldBe([new BriefInput("keeper.json", CatalogFile.Hash(inputs[0])), new BriefInput("bitwarden.json", CatalogFile.Hash(inputs[1]))]);
    }

    [Fact]
    public void Brief_lists_suggested_links()
    {
        string[] inputs =
        [
            Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com", modified: false))),
            Write(directory, "bitwarden.json", BitwardenJson(
                BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com", revised: false),
                BitwardenLogin("bw-2", "Example", "bob", "fixture-bob-password", "https://example.com"))),
        ];

        (BriefDocument brief, var result) = BriefRun.Build(inputs);

        result.Match.Suggestions.ShouldNotBeEmpty();
        brief.Suggestions.ShouldBe(result.Match.Suggestions);
    }
}
