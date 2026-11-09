namespace PandaBear.Secrets.Tool;

/// <summary>The exit codes of every command.</summary>
internal static class ExitCodes
{
    /// <summary>Success, or a preview that changed nothing.</summary>
    public const int Success = 0;

    /// <summary>A check ran and found a file whose recipients are not exactly its rule's recipients.</summary>
    public const int Differences = 1;

    /// <summary>The result cannot be known or the request was refused: invalid configuration or input, a missing tool, a failed tool, a missing <c>--force</c>, an existing output.</summary>
    public const int Failure = 2;
}
