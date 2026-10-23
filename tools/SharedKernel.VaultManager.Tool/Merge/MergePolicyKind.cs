namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>How the merger picks a winner when members hold different values for a field.</summary>
internal enum MergePolicyKind
{
    /// <summary>The member with the latest modification time wins. Missing or tied timestamps leave the field unresolved.</summary>
    NewestWins = 0,

    /// <summary>The member read from <see cref="MergePolicy.PreferredSourceFile"/> wins.</summary>
    PreferSource = 1,

    /// <summary>Every conflict stays unresolved until the user decides.</summary>
    Ask = 2,
}
