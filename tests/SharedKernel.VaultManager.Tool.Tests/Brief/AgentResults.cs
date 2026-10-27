using PandaBear.VaultManager.Tool.Analysis;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Brief;

/// <summary>Builds merge results with given groups, to check what agent decisions change.</summary>
internal static class AgentResults
{
    /// <summary>Creates a login named <c>Example</c> that originates from the given provider and ID.</summary>
    public static VaultItem Login(string id, string provider, string username, string password) => new()
    {
        Type = VaultItemType.Login,
        Name = "Example",
        Login = new LoginData { Username = username, Password = password, Uris = [new LoginUri("https://example.com")] },
        Origin = new ItemOrigin(provider, provider + ".json", id, null),
    };

    /// <summary>Returns the origin key of an item: provider, source file, and original ID.</summary>
    public static string Key(VaultItem item) => item.Origin.Provider + "|" + item.Origin.SourceFile + "|" + item.Origin.OriginalId;

    /// <summary>Builds a pipeline result with a match group for each given key and its members, and nothing else.</summary>
    public static PipelineResult Result(params (string Key, VaultItem[] Members)[] groups)
    {
        MatchGroup[] matched = [.. groups.Select(group => new MatchGroup(group.Key, group.Members.Length == 1 ? MatchStatus.Unique : MatchStatus.Different, group.Members, [.. group.Members.Select(Key)]))];
        return new PipelineResult
        {
            Merged = new VaultExport([.. groups.Select(group => group.Members[0])]),
            Match = new MatchResult(matched, []),
            Unresolved = [],
            MergeReport = new MergeReport(),
            Findings = [],
            PlacementProblems = [],
            PasswordHealth = new PasswordHealthReport([]),
            ManualAttachments = [],
            WriteErrors = [],
            WriteWarnings = [],
            ReadWarnings = [],
        };
    }
}
