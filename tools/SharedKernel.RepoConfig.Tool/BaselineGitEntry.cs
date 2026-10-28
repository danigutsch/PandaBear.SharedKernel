namespace PandaBear.RepoConfig.Tool;

/// <summary>A Git setting with how the repository's value compares with the desired one.</summary>
/// <param name="Key">The setting key.</param>
/// <param name="Value">The desired value.</param>
/// <param name="Status"><see cref="BaselineStatus.Current"/>, <see cref="BaselineStatus.Missing"/> (not set), or <see cref="BaselineStatus.Unmanaged"/> (a different value).</param>
/// <param name="OverwriteExisting">Whether the configuration allows replacing a different value.</param>
internal sealed record BaselineGitEntry(string Key, string Value, BaselineStatus Status, bool OverwriteExisting)
{
    /// <summary>Gets whether <c>baseline apply</c> must not replace the value.</summary>
    public bool IsConflict => Status == BaselineStatus.Unmanaged && !OverwriteExisting;
}
