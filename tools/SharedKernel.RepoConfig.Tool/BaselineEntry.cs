namespace PandaBear.RepoConfig.Tool;

/// <summary>A managed file with its desired content and how its current content compares.</summary>
/// <param name="Path">Repository-relative path.</param>
/// <param name="FullPath">Absolute path.</param>
/// <param name="Status">Comparison result.</param>
/// <param name="Desired">The content that the baseline produces, marker included, with LF line endings.</param>
/// <param name="Existing">The current content as read from disk, or <see langword="null"/> when the file is missing.</param>
/// <param name="OverwriteExisting">Whether the configuration allows replacing a modified or unmanaged file.</param>
/// <param name="Executable">Whether the file must have the owner execute permission on POSIX systems.</param>
internal sealed record BaselineEntry(
    string Path,
    string FullPath,
    BaselineStatus Status,
    string Desired,
    string? Existing,
    bool OverwriteExisting,
    bool Executable = false)
{
    /// <summary>Gets whether <c>baseline apply</c> must not replace the file.</summary>
    public bool IsConflict => Status is BaselineStatus.Modified or BaselineStatus.Unmanaged && !OverwriteExisting;
}
