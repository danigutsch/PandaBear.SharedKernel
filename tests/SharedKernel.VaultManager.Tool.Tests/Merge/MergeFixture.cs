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
    /// <summary>The source file name of the Keeper item.</summary>
    public const string KeeperFile = "keeper.json";

    /// <summary>The source file name of the Bitwarden item.</summary>
    public const string BitwardenFile = "bitwarden.json";

    /// <summary>The origin key of the Keeper item with original ID <c>k1</c>.</summary>
    public const string KeeperKey = "keeper|keeper.json|k1";

    /// <summary>The origin key of the Bitwarden item with original ID <c>b1</c>.</summary>
    public const string BitwardenKey = "bitwarden|bitwarden.json|b1";

    /// <summary>The default login username shared by both fixture items.</summary>
    public const string Username = "alice";

    /// <summary>The login address shared by both fixture items.</summary>
    public const string ExampleUri = "https://example.com/login";

    /// <summary>The Keeper item's modification time, the earliest of the fixture timestamps.</summary>
    public static readonly DateTimeOffset Older = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>A timestamp between <see cref="Older"/> and <see cref="Newer"/>.</summary>
    public static readonly DateTimeOffset Middle = new(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>The Bitwarden item's revision date, later than <see cref="Older"/>.</summary>
    public static readonly DateTimeOffset Newer = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>The fixed clock time that merges use for timestamps they create, such as password history entries.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Creates login data with the given password, TOTP, and username and the <see cref="ExampleUri"/> address.</summary>
    public static LoginData Login(string? password = null, string? totp = null, string? username = Username) =>
        new()
        {
            Username = username,
            Password = password,
            Totp = totp,
            Uris = [new LoginUri(ExampleUri)]
        };

    /// <summary>Creates the older Keeper login item named "Example" with original ID <c>k1</c> and a modification time of <see cref="Older"/>.</summary>
    public static VaultItem Keeper(LoginData login) => new()
    {
        Type = VaultItemType.Login,
        Name = "Example",
        Login = login,
        Origin = new ItemOrigin("keeper", KeeperFile, "k1", Older),
    };

    /// <summary>Creates the newer Bitwarden login item named "Example" with original ID <c>b1</c> and a revision date of <see cref="Newer"/>.</summary>
    public static VaultItem Bitwarden(LoginData login) => new()
    {
        Type = VaultItemType.Login,
        Name = "Example",
        Login = login,
        RevisionDate = Newer,
        Origin = new ItemOrigin("bitwarden", BitwardenFile, "b1", null),
    };

    /// <summary>Returns a copy of the item with both its revision date and its origin modification time cleared.</summary>
    public static VaultItem WithoutTimestamp(VaultItem item) =>
        item with
        {
            RevisionDate = null,
            Origin = item.Origin with
            {
                Modified = null
            }
        };

    /// <summary>Creates a card item named "Visa" with the given origin, revision date, and card data.</summary>
    public static VaultItem Card(ItemOrigin origin, DateTimeOffset revision, CardData card) => new()
    {
        Type = VaultItemType.Card,
        Name = "Visa",
        Card = card,
        RevisionDate = revision,
        Origin = origin,
    };

    /// <summary>Matches duplicates among the items, applying the given link and split overrides.</summary>
    public static MatchResult Match(MatchOverrides? overrides, params VaultItem[] items) => DuplicateMatcher.Match(items, overrides);

    /// <summary>Merges the items with the default policy, no decisions, and no overrides.</summary>
    public static MergeResult Run(params VaultItem[] items) => Run(new MergePolicy(), null, null, items);

    /// <summary>Merges the items with the given policy, no decisions, and no overrides.</summary>
    public static MergeResult Run(MergePolicy policy, params VaultItem[] items) => Run(policy, null, null, items);

    /// <summary>Matches the items with the overrides, then merges the groups with the policy and decisions using a clock fixed at <see cref="Now"/>.</summary>
    public static MergeResult Run(MergePolicy policy, MergeDecisions? decisions, MatchOverrides? overrides, params VaultItem[] items)
    {
        var groups = DuplicateMatcher.Match(items, overrides).Groups;
        return new Merger(new FixedTimeProvider(Now)).Merge(groups, policy, decisions);
    }

    /// <summary>Creates merge decisions holding one field choice for the group and no overrides.</summary>
    public static MergeDecisions Decide(string groupKey, FieldChoice choice) =>
        new([new GroupDecision(groupKey, [choice])], MatchOverrides.None);

    /// <summary>Creates overrides that force the Keeper and Bitwarden fixture items into one group.</summary>
    public static MatchOverrides LinkKeeperAndBitwarden() => new([new OriginKeyPair(KeeperKey, BitwardenKey)], []);
}
