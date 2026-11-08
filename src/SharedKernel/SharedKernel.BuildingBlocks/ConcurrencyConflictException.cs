namespace PandaBear.BuildingBlocks;

/// <summary>
/// Signals that a save found the row changed or gone since it was read, so the write would have overwritten or targeted data
/// the caller never saw. Callers typically reload and retry or report a conflict. The message never contains data values, and
/// the provider exception is kept as the inner exception for diagnostics.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    private const string DefaultMessage = "The data was changed or was deleted since it was read.";

    /// <summary>Initializes a new instance with the default message.</summary>
    public ConcurrencyConflictException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The message; it must not contain data values.</param>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and the provider exception.</summary>
    /// <param name="message">The message; it must not contain data values.</param>
    /// <param name="innerException">The provider exception, kept for diagnostics.</param>
    public ConcurrencyConflictException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance with the default message and the provider exception.</summary>
    /// <param name="innerException">The provider exception, kept for diagnostics.</param>
    public ConcurrencyConflictException(Exception? innerException)
        : base(DefaultMessage, innerException)
    {
    }
}
