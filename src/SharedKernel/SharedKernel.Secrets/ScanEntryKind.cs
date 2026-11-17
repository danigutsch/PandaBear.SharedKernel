namespace PandaBear.Secrets;

/// <summary>What a walk found at a path.</summary>
internal enum ScanEntryKind
{
    /// <summary>A regular file.</summary>
    File,

    /// <summary>A symbolic link, which is never followed.</summary>
    Link,

    /// <summary>A directory that cannot be listed.</summary>
    UnreadableDirectory,
}
