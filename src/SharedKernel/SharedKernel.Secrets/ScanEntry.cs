namespace PandaBear.Secrets;

/// <summary>One entry that the repository walk found.</summary>
/// <param name="RelativePath">The path relative to the root, with <c>/</c> separators.</param>
/// <param name="AbsolutePath">The absolute path.</param>
/// <param name="Kind">What the entry is.</param>
/// <param name="Problem">Why the entry could not be examined, for an unreadable one.</param>
internal sealed record ScanEntry(string RelativePath, string AbsolutePath, ScanEntryKind Kind, string? Problem = null);
