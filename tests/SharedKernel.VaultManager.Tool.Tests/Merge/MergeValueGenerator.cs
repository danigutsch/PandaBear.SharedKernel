using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

/// <summary>Generates Keeper and Bitwarden item pairs from small value pools, so equal, absent, and conflicting values all occur.</summary>
internal static class MergeValueGenerator
{
    private static readonly string?[] Texts = [null, "", "   ", "alpha", "beta", "gamma"];
    private static readonly string?[] Totps = [null, " ", "JBSWY3DPEHPK3PXP", "KRSXG5CTMVRXEZLU"];
    private static readonly string?[] Folders = [null, "Work", "Home"];
    private static readonly string[] Names = ["Example", "Example two"];
    private static readonly string[] Hosts = ["https://example.com", "https://one.example.com", "https://two.example.com"];
    private static readonly DateTimeOffset?[] Times = [null, MergeFixture.Older, MergeFixture.Newer];

    public static (VaultItem First, VaultItem Second) Pair(Random random)
    {
        var first = Item(random) with
        {
            Origin = new ItemOrigin("keeper", MergeFixture.KeeperFile, "k1", Pick(random, Times))
        };
        var second = Item(random) with
        {
            Origin = new ItemOrigin("bitwarden", MergeFixture.BitwardenFile, "b1", null),
            RevisionDate = Pick(random, Times)
        };
        return (first, second);
    }

    public static MergePolicy Policy(Random random) => random.Next(4) switch
    {
        0 => new MergePolicy(MergePolicyKind.Ask),
        1 => new MergePolicy(MergePolicyKind.PreferSource, MergeFixture.KeeperFile),
        _ => new MergePolicy(),
    };

    private static VaultItem Item(Random random) => new()
    {
        Type = VaultItemType.Login,
        Name = Pick(random, Names),
        FolderPath = Pick(random, Folders),
        Notes = Pick(random, Texts),
        Favorite = random.Next(2) == 0,
        Reprompt = random.Next(2) == 0,
        Login = new LoginData
        {
            Username = Pick(random, Texts),
            Password = Pick(random, Texts),
            Totp = Pick(random, Totps),
            Uris = [new LoginUri(Pick(random, Hosts))],
            Fido2Credentials = random.Next(2) == 0 ? [] : [new Fido2Credential { CredentialId = "cred-" + random.Next(3), KeyValue = "key", RpId = "example.com" }],
        },
        Fields = random.Next(2) == 0 ? [] : [new CustomField("Field", Pick(random, Texts), CustomFieldType.Hidden)],
        PasswordHistory = random.Next(2) == 0 ? [] : [new PasswordHistoryEntry("history-" + random.Next(3), MergeFixture.Older)],
        Origin = new ItemOrigin("placeholder", "placeholder.json", null, null),
    };

    private static T Pick<T>(Random random, T[] values) => values[random.Next(values.Length)];
}
