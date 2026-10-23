using PandaBear.VaultManager.Tool.Brief;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Brief;

public sealed class BriefFileTests : IDisposable
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string directory = Directory.CreateTempSubdirectory("vault-brief-file-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Brief_round_trips_and_is_written_readable_only_by_its_owner()
    {
        var brief = new BriefDocument(
            BriefFile.CurrentVersion,
            [new BriefInput("keeper.json", "4f2c"), new BriefInput("bitwarden.json", "9a1b")],
            new BriefSummary(3, 1, 0, 1, 1, 0, 1, 2, 0),
            [
                new BriefGroup(
                    "4943740a016f2549",
                    VaultItemType.Login,
                    [
                        new BriefMember("keeper|keeper.json|rec-1", "keeper.json", "Café ‮example", "Work", "example.com", null, null, Newest: false,
                            [new BriefFinding("notes-password@notes:10", SecretKind.Password, Confidence.Medium, "notes", 2, "login.password", PreAccepted: false, NeedsDecision: true, AgentMayAccept: true, AgentMayReject: true)]),
                        new BriefMember("bitwarden|bitwarden.json|bw-1", "bitwarden.json", "Example", null, "example.com", "alice", DateTimeOffset.UnixEpoch, Newest: true, []),
                    ],
                    [
                        new BriefField("login.password", NeedsDecision: true, AgentMayDecide: true,
                            [new BriefHolder("keeper|keeper.json|rec-1", Newest: false, Weak: true, Reused: false, InOtherHistory: false)]),
                        new BriefField("login.uris", NeedsDecision: false, AgentMayDecide: false,
                            [new BriefHolder("bitwarden|bitwarden.json|bw-1", Newest: true, Values: ["https://***@example.com/"])]),
                    ]),
            ],
            [new MatchSuggestion(MatchSuggestionKind.SameTitle, "keeper|keeper.json|rec-2", "bitwarden|bitwarden.json|bw-2", "Mail", "Mail")]);
        string path = Path.Combine(directory, "brief.json");

        BriefFile.Write(path, brief, TestContext.Current.CancellationToken);
        string json = File.ReadAllText(path);
        var reread = BriefFile.Read(path);

        BriefFile.Serialize(reread).ShouldBe(json);
        json.ShouldContain("Café \\u202Eexample", StringComparison.Ordinal);
        json.ShouldContain("\"kind\": \"Password\"", StringComparison.Ordinal);
        json.ShouldNotContain("\"value\"", StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(OwnerOnly);
        }
    }

    [Theory]
    [InlineData("""{"version":1,"inputs":[],"summary":{"items":0,"mergedGroups":0,"conflictsResolvedByPolicy":0,"fieldsNeedingDecision":0,"findings":0,"findingsApplied":0,"findingsNeedingDecision":0,"passwordIssues":0,"attachmentsToUpload":0},"groups":[],"suggestions":[],"extra":1}""", "$.extra")]
    [InlineData("""{"version":1,"inputs":[],"summary":{"items":0,"mergedGroups":0,"conflictsResolvedByPolicy":0,"fieldsNeedingDecision":0,"findings":0,"findingsApplied":0,"findingsNeedingDecision":0,"passwordIssues":0,"attachmentsToUpload":0},"groups":[],"groups":[],"suggestions":[]}""", "$.groups")]
    [InlineData("""{"version":1,"inputs":[],"groups":[],"suggestions":[]}""", "$")]
    [InlineData("""{"version":2,"inputs":[],"summary":{"items":0,"mergedGroups":0,"conflictsResolvedByPolicy":0,"fieldsNeedingDecision":0,"findings":0,"findingsApplied":0,"findingsNeedingDecision":0,"passwordIssues":0,"attachmentsToUpload":0},"groups":[],"suggestions":[]}""", "version 2")]
    public void Brief_rejects_unknown_repeated_and_missing_properties_and_other_versions(string json, string named)
    {
        var action = () => BriefFile.Deserialize(json);

        action.ShouldThrow<InvalidDataException>().Message.ShouldContain(named, StringComparison.Ordinal);
    }
}
