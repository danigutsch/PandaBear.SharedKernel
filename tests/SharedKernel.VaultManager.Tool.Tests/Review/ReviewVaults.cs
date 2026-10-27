using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Review;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>
/// Builds scanned vaults and review sessions from synthetic items. Secret-shaped values are built at test runtime; the
/// secret scanner runs on the items as it does after reading a file.
/// </summary>
internal static class ReviewVaults
{
    /// <summary>Password given for the encrypted export in review sessions.</summary>
    public const string ExportPassword = "fixture export password";
    /// <summary>Password of the synthetic Keeper login.</summary>
    public const string KeeperPassword = "fixture-keeper-password";
    /// <summary>Password of the synthetic Bitwarden login.</summary>
    public const string BitwardenPassword = "fixture-bitwarden-password";
    /// <summary>Source file name given to Keeper items.</summary>
    public const string KeeperFile = "keeper.json";
    /// <summary>Source file name given to Bitwarden items.</summary>
    public const string BitwardenFile = "bitwarden.json";
    /// <summary>Origin key of the Keeper item <c>rec-1</c>.</summary>
    public const string KeeperKey = "keeper|keeper.json|rec-1";
    /// <summary>Origin key of the Bitwarden item <c>bw-1</c>.</summary>
    public const string BitwardenKey = "bitwarden|bitwarden.json|bw-1";

    /// <summary>Builds a Keeper login item from <see cref="KeeperFile"/>.</summary>
    public static VaultItem KeeperLogin(string id, string name, string? username, string? password, string? uri, DateTimeOffset? modified = null) =>
        Login("keeper", KeeperFile, id, name, username, password, uri, modified);

    /// <summary>Builds a Bitwarden login item from <see cref="BitwardenFile"/>.</summary>
    public static VaultItem BitwardenLogin(string id, string name, string? username, string? password, string? uri, DateTimeOffset? modified = null) =>
        Login("bitwarden", BitwardenFile, id, name, username, password, uri, modified);

    /// <summary>Builds a login item with an optional single URI, originating from the given provider and source file.</summary>
    public static VaultItem Login(string provider, string sourceFile, string id, string name, string? username, string? password, string? uri, DateTimeOffset? modified) => new()
    {
        Type = VaultItemType.Login,
        Name = name,
        Login = new LoginData { Username = username, Password = password, Uris = uri is null ? [] : [new LoginUri(uri)] },
        Origin = new ItemOrigin(provider, sourceFile, id, modified),
    };

    /// <summary>Builds a Keeper secure note item with the given notes.</summary>
    public static VaultItem KeeperNote(string id, string name, string notes) => new()
    {
        Type = VaultItemType.SecureNote,
        Name = name,
        Notes = notes,
        Origin = new ItemOrigin("keeper", KeeperFile, id, PipelineInputs.KeeperModified),
    };

    /// <summary>Returns the origin key of <paramref name="item"/> as provider, source file, and original id joined by pipes.</summary>
    public static string OriginKey(VaultItem item) => item.Origin.Provider + "|" + item.Origin.SourceFile + "|" + item.Origin.OriginalId;

    /// <summary>Scans items as if they were read from files in <paramref name="directory"/>, named after their source files.</summary>
    public static ScannedVault Scan(string directory, params VaultItem[] items)
    {
        IReadOnlyList<string> keys = OriginKeys.Compute(items);
        SourceItem[] sources = [.. items.Select((item, index) => new SourceItem(item, keys[index], Path.Combine(directory, item.Origin.SourceFile)))];
        string[] inputs = [.. sources.Select(source => source.InputPath).Distinct(StringComparer.Ordinal)];
        return VaultPipeline.Scan(new LoadedVault(inputs, sources, []));
    }

    /// <summary>Builds a review session over <paramref name="scanned"/>, defaulting to no decisions and no files.</summary>
    public static ReviewSession Session(ScannedVault scanned, MergeDecisions? decisions = null, ReviewFiles? files = null) =>
        new(new TestPrompts(ExportPassword).Environment(), scanned, decisions ?? MergeDecisions.None, files ?? ReviewFiles.None);

    /// <summary>The same login in Keeper and Bitwarden with different passwords and no modification times, so the password needs a decision.</summary>
    public static ScannedVault ConflictingLogins(string directory) => Scan(
        directory,
        KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com"),
        BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com"));

    /// <summary>A Keeper login whose notes hold pre-accepted recovery codes and a password that needs accepting.</summary>
    public static VaultItem CodesAndPasswordInNotes(string id, string[] codes, string notesPassword) => KeeperLogin(id, "GitHub", "alice", null, "https://github.com", PipelineInputs.KeeperModified) with
    {
        Notes = PipelineSecrets.RecoveryNotes(codes, notesPassword),
    };

    /// <summary>Returns the one review group that contains the item with <paramref name="originKey"/>.</summary>
    public static ReviewGroup GroupOf(ReviewSession session, string originKey) =>
        session.Groups(GroupFilter.All, null).ShouldHaveSingleItem(group => group.MemberKeys.Contains(originKey, StringComparer.Ordinal));

    /// <summary>Returns the one field of group <paramref name="groupKey"/> at <paramref name="fieldPath"/>.</summary>
    public static ReviewField FieldOf(ReviewSession session, string groupKey, string fieldPath) =>
        session.Fields(groupKey).ShouldHaveSingleItem(field => string.Equals(field.FieldPath, fieldPath, StringComparison.Ordinal));
}
