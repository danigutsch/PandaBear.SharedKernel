using PandaBear.Vault.Model;

namespace PandaBear.Vault.Tests.Pipeline;

/// <summary>Builds SSH key items read from a Keeper export.</summary>
internal static class SshKeyItems
{
    // The last-modified time of the Keeper records in the pipeline tests, 2024-03-01 12:00 UTC.
    private static readonly DateTimeOffset KeeperModified = new(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Creates an SSH key item named "Deploy key" holding the given key, originating from Keeper record <c>rec-ssh</c>.</summary>
    public static VaultItem Item(SshKeyData key) => new()
    {
        Type = VaultItemType.SshKey,
        Name = "Deploy key",
        SshKey = key,
        Origin = new ItemOrigin("keeper", "keeper.json", "rec-ssh", KeeperModified),
    };
}
