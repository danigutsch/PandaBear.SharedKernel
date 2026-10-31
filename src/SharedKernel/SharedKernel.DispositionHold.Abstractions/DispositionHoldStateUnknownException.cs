namespace PandaBear.DispositionHold;

/// <summary>
/// Thrown when a guard cannot determine hold state. Callers must treat it as a refusal to dispose of data.
/// </summary>
public sealed class DispositionHoldStateUnknownException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public DispositionHoldStateUnknownException()
        : base("Disposition hold state could not be determined; disposition must be refused.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public DispositionHoldStateUnknownException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The underlying failure.</param>
    public DispositionHoldStateUnknownException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
