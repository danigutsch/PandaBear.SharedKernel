using System.Diagnostics.CodeAnalysis;

namespace PandaBear.Results;

/// <summary>
/// Common contract of <see cref="Result"/> and <see cref="Result{T}"/>, so generic code such as an open-generic
/// pipeline behavior can read the outcome and short-circuit with a failure without knowing the concrete result shape.
/// </summary>
/// <remarks>
/// <see cref="Result"/> and <see cref="Result{T}"/> are the intended implementers; the interface is a constraint for generic code,
/// not an extension point.
/// <para>
/// A <see langword="default"/> instance of either struct was never produced by an operation, so it is invalid. Its
/// <see cref="Status"/> is <see cref="ResultStatus.Unknown"/>, <see cref="IsSuccess"/> and <see cref="IsFailure"/> are both
/// <see langword="false"/>, and <c>Match</c> throws <see cref="InvalidOperationException"/>. Generic code should test
/// <see cref="IsSuccess"/> for the success path and treat every other state as not successful; <c>!IsFailure</c> does not mean success.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">The implementing result type.</typeparam>
public interface IResultLike<TSelf>
    where TSelf : IResultLike<TSelf>
{
    /// <summary>Gets the status of the result.</summary>
    ResultStatus Status
    {
        get;
    }

    /// <summary>Gets a value indicating whether the result is successful.</summary>
    bool IsSuccess
    {
        get;
    }

    /// <summary>Gets a value indicating whether the result is a failure.</summary>
    bool IsFailure
    {
        get;
    }

    /// <summary>Gets the error of a failed result, or <see langword="null"/> when the result is not a failure.</summary>
    ResultError? ErrorDetails
    {
        get;
    }

    /// <summary>Gets the error of a failed result.</summary>
    /// <param name="currentError">The error when the result is a failure; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the result is a failure.</returns>
    bool TryGetError([NotNullWhen(true)] out ResultError? currentError);

    /// <summary>Creates a failure from an existing error; the status is derived from <see cref="ResultError.Code"/>.</summary>
    /// <param name="resultError">The error to carry.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resultError"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The error is an invalid error without field details.</exception>
    static abstract TSelf FromError(ResultError resultError);

    /// <summary>Creates an invalid result with one field message.</summary>
    /// <param name="detail">The summary of the failure.</param>
    /// <param name="field">The invalid field.</param>
    /// <param name="message">The message for the field.</param>
    /// <returns>The invalid result.</returns>
    static abstract TSelf Invalid(string detail, string field, string message);

    /// <summary>Creates an invalid result with messages per field.</summary>
    /// <param name="detail">The summary of the failure.</param>
    /// <param name="validationErrors">The messages per field; must not be empty.</param>
    /// <returns>The invalid result.</returns>
    static abstract TSelf Invalid(string detail, IReadOnlyDictionary<string, string[]> validationErrors);

    /// <summary>Creates a not-found result.</summary>
    /// <param name="detail">The summary of the failure.</param>
    /// <returns>The not-found result.</returns>
    static abstract TSelf NotFound(string detail);

    /// <summary>Creates an unauthorized result.</summary>
    /// <param name="detail">The summary of the failure.</param>
    /// <returns>The unauthorized result.</returns>
    static abstract TSelf Unauthorized(string detail);

    /// <summary>Creates a forbidden result.</summary>
    /// <param name="detail">The summary of the failure.</param>
    /// <returns>The forbidden result.</returns>
    static abstract TSelf Forbidden(string detail);

    /// <summary>Creates a conflict result.</summary>
    /// <param name="detail">The summary of the failure.</param>
    /// <returns>The conflict result.</returns>
    static abstract TSelf Conflict(string detail);

    /// <summary>Creates a generic error result.</summary>
    /// <param name="detail">The summary of the failure.</param>
    /// <returns>The error result.</returns>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Mirrors Result.Error; the member is only reachable through a generic constraint.")]
    static abstract TSelf Error(string detail);

    /// <summary>Creates a critical error result.</summary>
    /// <param name="detail">The summary of the failure.</param>
    /// <returns>The critical error result.</returns>
    static abstract TSelf CriticalError(string detail);

    /// <summary>Creates an unavailable result.</summary>
    /// <param name="detail">The summary of the failure.</param>
    /// <returns>The unavailable result.</returns>
    static abstract TSelf Unavailable(string detail);
}
