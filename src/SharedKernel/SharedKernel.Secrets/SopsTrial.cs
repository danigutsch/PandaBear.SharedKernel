namespace PandaBear.Secrets;

/// <summary>The outcome of a trial decryption and a short reason when it failed.</summary>
public sealed class SopsTrial
{
    /// <summary>Creates an outcome.</summary>
    /// <param name="outcome">What the trial found.</param>
    /// <param name="detail">A short reason that carries no plaintext.</param>
    /// <exception cref="ArgumentNullException"><paramref name="detail"/> is <see langword="null"/>.</exception>
    public SopsTrial(SopsTrialOutcome outcome, string detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        Outcome = outcome;
        Detail = detail;
    }

    /// <summary>Gets what the trial found.</summary>
    public SopsTrialOutcome Outcome
    {
        get;
    }

    /// <summary>Gets a short reason; empty after a successful decryption.</summary>
    public string Detail
    {
        get;
    }
}
