using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Builds SSH key items read from a Keeper export.</summary>
internal static class SshKeyItems
{
    /// <summary>Creates an SSH key item named "Deploy key" holding the given key, originating from Keeper record <c>rec-ssh</c>.</summary>
    public static VaultItem Item(SshKeyData key) => new()
    {
        Type = VaultItemType.SshKey,
        Name = "Deploy key",
        SshKey = key,
        Origin = new ItemOrigin("keeper", "keeper.json", "rec-ssh", PipelineInputs.KeeperModified),
    };
}
