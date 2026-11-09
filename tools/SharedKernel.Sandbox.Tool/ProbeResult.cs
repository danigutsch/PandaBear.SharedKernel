namespace PandaBear.Sandbox.Tool;

/// <summary>The outcome of one probe, with the reason and what it means for the user.</summary>
/// <param name="Id">A short stable name such as <c>unix-socket</c>.</param>
/// <param name="Kind">Which way the outcome is good.</param>
/// <param name="Outcome">What the probe found.</param>
/// <param name="Required">Whether a bad outcome fails the run; otherwise it is only reported.</param>
/// <param name="Detail">Why, in one line, with no path or secret in it.</param>
/// <param name="Advice">What to do or what breaks, shown when the outcome is bad; <see langword="null"/> when there is nothing to add.</param>
internal sealed record ProbeResult(string Id, ProbeKind Kind, ProbeOutcome Outcome, bool Required, string Detail, string? Advice = null)
{
    /// <summary>Gets whether the outcome is the good one for its kind.</summary>
    public bool IsGood => Kind == ProbeKind.Capability ? Outcome == ProbeOutcome.Allowed : Outcome == ProbeOutcome.Denied;

    /// <summary>Gets whether the probe could not run, so it says nothing either way.</summary>
    public bool IsSkipped => Outcome == ProbeOutcome.Skipped;

    /// <summary>Gets whether this result fails the run: a required probe that ran and had the bad outcome.</summary>
    public bool Fails => Required && !IsGood && !IsSkipped;

    /// <summary>Gets the word for the outcome: a capability is allowed or denied, a protection is blocked or exposed.</summary>
    public string Word => (Kind, Outcome) switch
    {
        (_, ProbeOutcome.Skipped) => "skipped",
        (_, ProbeOutcome.Failed) => "failed",
        (ProbeKind.Protection, ProbeOutcome.Denied) => "blocked",
        (ProbeKind.Protection, ProbeOutcome.Allowed) => "exposed",
        (_, ProbeOutcome.Allowed) => "allowed",
        _ => "denied",
    };
}
