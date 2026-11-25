namespace SharedKernel.Secrets;

/// <summary>The exit codes that every <see cref="SecretsRepository"/> operation returns.</summary>
public static class SecretsExitCodes
{
    /// <summary>Success, or a preview that changed nothing.</summary>
    public const int Success = 0;

    /// <summary>A check ran and found a difference: recipients that are not exactly the rule's, a weakened file, a failed proof, an uncovered file, or a changed label.</summary>
    public const int Differences = 1;

    /// <summary>The result cannot be known or the request was refused: invalid configuration or input, a missing or failed program, a missing <c>--force</c>, or an existing output.</summary>
    public const int Failure = 2;
}
