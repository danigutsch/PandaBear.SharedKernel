namespace PandaBear.BuildingBlocks;

/// <summary>
/// Signals that a save violated a uniqueness rule that the persistence layer enforces, typically because a concurrent
/// request won the race that an earlier existence check could not prevent. It names the application's rule, never a
/// provider constraint, so callers stay independent of the database.
/// </summary>
/// <param name="rule">The application-defined name of the violated rule.</param>
/// <param name="innerException">The provider exception, kept for diagnostics.</param>
public sealed class UniqueConstraintViolationException(string rule, Exception? innerException)
    : Exception($"The unique rule '{rule}' was violated.", innerException)
{
    /// <summary>Initializes a new instance for a rule.</summary>
    /// <param name="rule">The application-defined name of the violated rule.</param>
    public UniqueConstraintViolationException(string rule)
        : this(rule, null)
    {
    }

    /// <summary>Initializes a new instance with no rule. Required for exception conventions; prefer the rule constructors.</summary>
    public UniqueConstraintViolationException()
        : this("unknown")
    {
    }

    /// <summary>Gets the application-defined name of the violated rule.</summary>
    public string Rule { get; } = rule;
}
