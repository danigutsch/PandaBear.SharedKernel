namespace PandaBear.Blazor;

/// <summary>
/// Counts failures that application code caught and reported, so handled and unhandled exceptions share one metric series.
/// </summary>
/// <remarks>
/// An implementation increments the <c>unhandled_exceptions_total</c> counter with the tag <c>handled=true</c>.
/// Implementations must not throw. Register none to report to the log only.
/// </remarks>
public interface IHandledFailureCounter
{
    /// <summary>Records one handled failure.</summary>
    void IncrementHandled();
}
