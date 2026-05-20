using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Results;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    private readonly ResultError? error;

    internal Result(ResultStatus status, ResultError? error)
    {
        Status = status;
        this.error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorDetails))]
    public bool IsSuccess => Status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent or ResultStatus.Accepted;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ErrorDetails))]
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the result status.
    /// </summary>
    public ResultStatus Status { get; }

    /// <summary>
    /// Gets the error details when the result failed.
    /// </summary>
    public ResultError? ErrorDetails => IsFailure ? error : null;

    /// <summary>
    /// Returns the current error details when the result failed.
    /// </summary>
    /// <param name="currentError">The current error details when the result failed; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the result failed.</returns>
    public bool TryGetError([NotNullWhen(true)] out ResultError? currentError)
    {
        currentError = ErrorDetails;
        return currentError is not null;
    }

    /// <summary>
    /// Projects the result into one of two result values.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="whenSuccess">Called when the result succeeded.</param>
    /// <param name="whenFailure">Called when the result failed.</param>
    /// <returns>The value produced by the matched branch.</returns>
    public TResult Match<TResult>(Func<TResult> whenSuccess, Func<ResultError, TResult> whenFailure)
    {
        ArgumentNullException.ThrowIfNull(whenSuccess);
        ArgumentNullException.ThrowIfNull(whenFailure);

        return TryGetError(out var currentError)
            ? whenFailure(currentError)
            : whenSuccess();
    }

    /// <summary>
    /// Creates a successful result with OK status.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result Ok() => new(ResultStatus.Ok, null);

    /// <summary>
    /// Creates a successful result with a value and OK status.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="value">The success value.</param>
    /// <returns>A successful result containing the value.</returns>
    public static Result<T> Ok<T>(T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);

        return new(ResultStatus.Ok, value, null);
    }

    /// <summary>
    /// Creates a successful result with NoContent status.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result NoContent() => new(ResultStatus.NoContent, null);

    /// <summary>
    /// Creates a successful result with a value and Created status.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="value">The success value.</param>
    /// <returns>A successful created result containing the value.</returns>
    public static Result<T> Created<T>(T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);

        return new(ResultStatus.Created, value, null);
    }

    /// <summary>
    /// Creates a successful result with Accepted status.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result Accepted() => new(ResultStatus.Accepted, null);

    /// <summary>
    /// Creates a successful result with a value and Accepted status.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="value">The success value.</param>
    /// <returns>A successful accepted result containing the value.</returns>
    public static Result<T> Accepted<T>(T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);

        return new(ResultStatus.Accepted, value, null);
    }

    /// <summary>
    /// Creates a failed invalid result with one validation error.
    /// </summary>
    /// <param name="detail">The error detail.</param>
    /// <param name="field">The invalid field name.</param>
    /// <param name="message">The field validation message.</param>
    /// <returns>A failed result.</returns>
    public static Result Invalid(string detail, string field, string message) =>
        new(ResultStatus.Invalid, CreateValidationError(detail, ResultErrorCodes.Invalid, field, message));

    /// <summary>
    /// Creates a failed invalid result with multiple validation errors.
    /// </summary>
    /// <param name="detail">The error detail.</param>
    /// <param name="validationErrors">The validation errors keyed by field name.</param>
    /// <returns>A failed result.</returns>
    public static Result Invalid(string detail, Dictionary<string, string[]> validationErrors) =>
        new(ResultStatus.Invalid, CreateValidationError(detail, ResultErrorCodes.Invalid, validationErrors));

    /// <summary>
    /// Creates a failed invalid result with one validation error.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="detail">The error detail.</param>
    /// <param name="field">The invalid field name.</param>
    /// <param name="message">The field validation message.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Invalid<T>(string detail, string field, string message)
        where T : notnull => new(ResultStatus.Invalid, default, CreateValidationError(detail, ResultErrorCodes.Invalid, field, message));

    /// <summary>
    /// Creates a failed invalid result with multiple validation errors.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="detail">The error detail.</param>
    /// <param name="validationErrors">The validation errors keyed by field name.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Invalid<T>(string detail, Dictionary<string, string[]> validationErrors)
        where T : notnull => new(ResultStatus.Invalid, default, CreateValidationError(detail, ResultErrorCodes.Invalid, validationErrors));

    /// <summary>
    /// Creates a failed not found result.
    /// </summary>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result NotFound(string detail) => new(ResultStatus.NotFound, CreateError(detail, ResultErrorCodes.NotFound));

    /// <summary>
    /// Creates a failed not found result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> NotFound<T>(string detail)
        where T : notnull => new(ResultStatus.NotFound, default, CreateError(detail, ResultErrorCodes.NotFound));

    /// <summary>
    /// Creates a failed unauthorized result.
    /// </summary>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result Unauthorized(string detail) => new(ResultStatus.Unauthorized, CreateError(detail, ResultErrorCodes.Unauthorized));

    /// <summary>
    /// Creates a failed unauthorized result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Unauthorized<T>(string detail)
        where T : notnull => new(ResultStatus.Unauthorized, default, CreateError(detail, ResultErrorCodes.Unauthorized));

    /// <summary>
    /// Creates a failed forbidden result.
    /// </summary>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result Forbidden(string detail) => new(ResultStatus.Forbidden, CreateError(detail, ResultErrorCodes.Forbidden));

    /// <summary>
    /// Creates a failed forbidden result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Forbidden<T>(string detail)
        where T : notnull => new(ResultStatus.Forbidden, default, CreateError(detail, ResultErrorCodes.Forbidden));

    /// <summary>
    /// Creates a failed conflict result.
    /// </summary>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result Conflict(string detail) => new(ResultStatus.Conflict, CreateError(detail, ResultErrorCodes.Conflict));

    /// <summary>
    /// Creates a failed conflict result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Conflict<T>(string detail)
        where T : notnull => new(ResultStatus.Conflict, default, CreateError(detail, ResultErrorCodes.Conflict));

    /// <summary>
    /// Creates a failed error result.
    /// </summary>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result Error(string detail) => new(ResultStatus.Error, CreateError(detail, ResultErrorCodes.Error));

    /// <summary>
    /// Creates a failed error result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Error<T>(string detail)
        where T : notnull => new(ResultStatus.Error, default, CreateError(detail, ResultErrorCodes.Error));

    /// <summary>
    /// Creates a failed critical error result.
    /// </summary>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result CriticalError(string detail) => new(ResultStatus.CriticalError, CreateError(detail, ResultErrorCodes.CriticalError));

    /// <summary>
    /// Creates a failed critical error result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> CriticalError<T>(string detail)
        where T : notnull => new(ResultStatus.CriticalError, default, CreateError(detail, ResultErrorCodes.CriticalError));

    /// <summary>
    /// Creates a failed unavailable result.
    /// </summary>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result Unavailable(string detail) => new(ResultStatus.Unavailable, CreateError(detail, ResultErrorCodes.Unavailable));

    /// <summary>
    /// Creates a failed unavailable result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="detail">The error detail.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Unavailable<T>(string detail)
        where T : notnull => new(ResultStatus.Unavailable, default, CreateError(detail, ResultErrorCodes.Unavailable));

    private static ResultError CreateError(string detail, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return new ResultError(detail, code);
    }

    private static ResultError CreateValidationError(string detail, string code, string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ResultError(detail, code, new Dictionary<string, string[]>
        {
            [field] = [message],
        });
    }

    private static ResultError CreateValidationError(string detail, string code, Dictionary<string, string[]> validationErrors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentNullException.ThrowIfNull(validationErrors);

        if (validationErrors.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(validationErrors), "Validation errors dictionary cannot be empty.");
        }

        var clonedValidationErrors = new Dictionary<string, string[]>(validationErrors.Count, StringComparer.Ordinal);
        foreach (var (field, messages) in validationErrors)
        {
            clonedValidationErrors[field] = [.. messages];
        }

        return new ResultError(detail, code, clonedValidationErrors);
    }

    /// <inheritdoc />
    public bool Equals(Result other) =>
        IsSuccess == other.IsSuccess &&
        Status == other.Status &&
        EqualityComparer<ResultError?>.Default.Equals(error, other.error);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(IsSuccess, Status, error);

    /// <inheritdoc />
    public override string ToString() =>
        IsSuccess
            ? $"Success: {Status}"
            : $"Failure: {Status} - {error?.Detail}";

    /// <summary>
    /// Determines whether two result instances are equal.
    /// </summary>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>
    /// Determines whether two result instances are not equal.
    /// </summary>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);
}
