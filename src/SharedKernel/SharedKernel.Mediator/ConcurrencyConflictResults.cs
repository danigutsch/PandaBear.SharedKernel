using PandaBear.Results;

namespace PandaBear.Mediator;

/// <summary>
/// Conflict results for a handler that finds the record it loaded has a newer version than the one the caller saw, before it saves anything.
/// </summary>
/// <remarks>
/// A change that lands between the load and the save is converted by <see cref="ConcurrencyConflictBehavior{TRequest, TResponse}"/>. These helpers
/// return the same <see cref="ResultStatus.Conflict"/> result and message, so a stale form and a lost race look identical to the caller.
/// </remarks>
public static class ConcurrencyConflictResults
{
    /// <summary>Creates the conflict result of a record that changed after the caller loaded it.</summary>
    /// <returns>A conflict result carrying <see cref="ConcurrencyConflictBehavior{TRequest, TResponse}.ConflictMessage"/>.</returns>
    public static Result Stale() => Result.Conflict(ConcurrencyConflictBehavior<IRequest<Result>, Result>.ConflictMessage);

    /// <summary>Creates the conflict result of a record that changed after the caller loaded it, for a handler that returns a value.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <returns>A conflict result carrying <see cref="ConcurrencyConflictBehavior{TRequest, TResponse}.ConflictMessage"/>.</returns>
    public static Result<T> Stale<T>()
        where T : notnull => Result.Conflict<T>(ConcurrencyConflictBehavior<IRequest<Result<T>>, Result<T>>.ConflictMessage);
}
