using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Builds SSH key items read from a Keeper export.</summary>
internal static class SshKeyItems
{
    public static VaultItem Item(SshKeyData key) => new()
    {
        Type = VaultItemType.SshKey,
        Name = "Deploy key",
        SshKey = key,
        Origin = new ItemOrigin("keeper", "keeper.json", "rec-ssh", PipelineInputs.KeeperModified),
    };
}
