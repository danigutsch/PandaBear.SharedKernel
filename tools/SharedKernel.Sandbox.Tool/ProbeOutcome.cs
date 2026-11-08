namespace PandaBear.Sandbox.Tool;

/// <summary>What a probe found.</summary>
internal enum ProbeOutcome
{
    /// <summary>The operation worked.</summary>
    Allowed,

    /// <summary>The operating system or the sandbox refused the operation.</summary>
    Denied,

    /// <summary>The operation failed for a reason other than a refusal.</summary>
    Failed,

    /// <summary>The probe could not run here, for example because the path does not exist.</summary>
    Skipped,
}
