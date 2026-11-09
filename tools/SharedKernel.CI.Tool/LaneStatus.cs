namespace PandaBear.CI.Tool;

/// <summary>How a lane of <c>local-check</c> ended.</summary>
internal enum LaneStatus
{
    /// <summary>The lane ran and found nothing wrong.</summary>
    Passed,

    /// <summary>The lane ran and found a problem, or a child process crashed.</summary>
    Failed,

    /// <summary>The lane could not run, and the report says why; <c>--strict</c> counts it as a failure.</summary>
    Skipped,
}
