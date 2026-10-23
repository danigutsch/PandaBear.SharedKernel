namespace PandaBear.VaultManager.Tool.Attachments;

/// <summary>Bounds applied while reading an archive.</summary>
internal sealed record ArchiveLimits
{
    /// <summary>Limits from the design: 1000 entries, 100 MiB expanded, 100:1 per entry, nesting depth 2.</summary>
    public static ArchiveLimits Default { get; } = new();

    /// <summary>Most entries, directories and nested archive entries included.</summary>
    public int MaxEntries { get; init; } = 1000;

    /// <summary>Most expanded bytes across all entries, nested archives included.</summary>
    public long MaxExpandedBytes { get; init; } = 100L * 1024 * 1024;

    /// <summary>Highest ratio of actual expanded bytes to compressed bytes for one entry.</summary>
    public int MaxCompressionRatio { get; init; } = 100;

    /// <summary>
    /// Expanded size below which the ratio is not enforced, so small, highly repetitive files such as blank forms are
    /// accepted. The total size limit still applies.
    /// </summary>
    public long RatioExemptBytes { get; init; } = 64 * 1024;

    /// <summary>Deepest archive-in-archive level: the outer archive is level 0.</summary>
    public int MaxNestingDepth { get; init; } = 2;
}
