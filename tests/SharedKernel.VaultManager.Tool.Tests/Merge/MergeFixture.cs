using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

/// <summary>
/// Fixed merge fixture: Keeper item K is older and carries <see cref="ItemOrigin.Modified"/>; Bitwarden item B is
/// newer and carries <see cref="VaultItem.RevisionDate"/>. Both share the same host and username.
/// </summary>
internal static class MergeFixture
{
    public const string KeeperFile = "keeper.json";
    public const string BitwardenFile = "bitwarden.json";
    public const string KeeperKey = "keeper|keeper.json|k1";
    public const string BitwardenKey = "bitwarden|bitwarden.json|b1";
    public const string Username = "alice";
    public const string ExampleUri = "https://example.com/login";

    public static readonly DateTimeOffset Older = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    public static readonly DateTimeOffset Middle = new(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);
    public static readonly DateTimeOffset Newer = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    public static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    public static LoginData Login(string? password = null, string? totp = null, string? username = Username) =>
        new() { Username = username, Password = password, Totp = totp, Uris = [new LoginUri(ExampleUri)] };

    public static VaultItem Keeper(LoginData login) => new()
    {
        Type = VaultItemType.Login,
        Name = "Example",
        Login = login,
        Origin = new ItemOrigin("keeper", KeeperFile, "k1", Older),
    };

    public static VaultItem Bitwarden(LoginData login) => new()
    {
        Type = VaultItemType.Login,
        Name = "Example",
        Login = login,
        RevisionDate = Newer,
        Origin = new ItemOrigin("bitwarden", BitwardenFile, "b1", null),
    };

    public static VaultItem WithoutTimestamp(VaultItem item) =>
        item with { RevisionDate = null, Origin = item.Origin with { Modified = null } };

    public static VaultItem Card(ItemOrigin origin, DateTimeOffset revision, CardData card) => new()
    {
        Type = VaultItemType.Card,
        Name = "Visa",
        Card = card,
        RevisionDate = revision,
        Origin = origin,
    };

    public static MatchResult Match(MatchOverrides? overrides, params VaultItem[] items) => DuplicateMatcher.Match(items, overrides);

    public static MergeResult Run(params VaultItem[] items) => Run(new MergePolicy(), null, null, items);

    public static MergeResult Run(MergePolicy policy, params VaultItem[] items) => Run(policy, null, null, items);

    public static MergeResult Run(MergePolicy policy, MergeDecisions? decisions, MatchOverrides? overrides, params VaultItem[] items)
    {
        var groups = DuplicateMatcher.Match(items, overrides).Groups;
        return new Merger(new FixedTimeProvider(Now)).Merge(groups, policy, decisions);
    }

    public static MergeDecisions Decide(string groupKey, FieldChoice choice) =>
        new([new GroupDecision(groupKey, [choice])], MatchOverrides.None);

    public static MatchOverrides LinkKeeperAndBitwarden() => new([new OriginKeyPair(KeeperKey, BitwardenKey)], []);
}
