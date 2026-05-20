using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Results;

/// <summary>
/// Represents the result of an operation that can succeed with a value or fail with an error.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
    where T : notnull
{
    private readonly T? value;
    private readonly ResultError? error;
    private const string UninitializedResultMessage = "Result status is not initialized.";
    private const string SuccessfulResultMustContainValueMessage = "Successful results must contain a value.";
    private const string FailedResultMustContainErrorDetailsMessage = "Failed results must contain error details.";

    internal Result(ResultStatus status, T? value, ResultError? error)
    {
        Status = status;
        this.value = value;
        this.error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(true, nameof(value))]
    [MemberNotNullWhen(false, nameof(ErrorDetails))]
    public bool IsSuccess => Status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.Accepted;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ErrorDetails))]
    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(false, nameof(value))]
    public bool IsFailure => Status is ResultStatus.Invalid or ResultStatus.Unauthorized or ResultStatus.Forbidden or ResultStatus.NotFound or ResultStatus.Conflict or ResultStatus.Error or ResultStatus.CriticalError or ResultStatus.Unavailable;

    /// <summary>
    /// Gets the result status.
    /// </summary>
    public ResultStatus Status { get; }

    /// <summary>
    /// Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is not successful.</exception>
    public T Value =>
        IsSuccess
            ? value ?? throw new InvalidOperationException(SuccessfulResultMustContainValueMessage)
            : throw new InvalidOperationException($"Cannot access Value of a failed result. Error: {error?.Detail}");

    /// <summary>
    /// Gets the error details when the result failed.
    /// </summary>
    public ResultError? ErrorDetails => IsFailure ? error : null;

    /// <summary>
    /// Returns the current success value when the result succeeded.
    /// </summary>
    /// <param name="currentValue">The current success value when the result succeeded; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the result succeeded.</returns>
    public bool TryGetValue([NotNullWhen(true)] out T? currentValue)
    {
        if (IsSuccess)
        {
            currentValue = value ?? throw new InvalidOperationException(SuccessfulResultMustContainValueMessage);
            return true;
        }

        currentValue = default;
        return false;
    }

    /// <summary>
    /// Maps the current success value into a new result value.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null success type.</typeparam>
    /// <param name="map">The projection to apply when the result succeeded.</param>
    /// <returns>The mapped result, or the original failure when the result failed.</returns>
    public Result<TResult> Map<TResult>(Func<T, TResult> map)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return TryGetValue(out var currentValue)
            ? Result.Ok(map(currentValue))
            : new Result<TResult>(Status, default, ErrorDetails);
    }

    /// <summary>
    /// Binds the current success value into another result.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null success type.</typeparam>
    /// <param name="bind">The projection to apply when the result succeeded.</param>
    /// <returns>The bound result, or the original failure when the result failed.</returns>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> bind)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        return TryGetValue(out var currentValue)
            ? bind(currentValue)
            : new Result<TResult>(Status, default, ErrorDetails);
    }

    /// <summary>
    /// Returns the current error details when the result failed.
    /// </summary>
    /// <param name="currentError">The current error details when the result failed; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the result failed.</returns>
    public bool TryGetError([NotNullWhen(true)] out ResultError? currentError)
    {
        if (IsFailure)
        {
            currentError = GetRequiredError();
            return true;
        }

        currentError = null;
        return false;
    }

    /// <summary>
    /// Projects the result into one of two result values.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="whenSuccess">Called when the result succeeded.</param>
    /// <param name="whenFailure">Called when the result failed.</param>
    /// <returns>The value produced by the matched branch.</returns>
    public TResult Match<TResult>(Func<T, TResult> whenSuccess, Func<ResultError, TResult> whenFailure)
    {
        ArgumentNullException.ThrowIfNull(whenSuccess);
        ArgumentNullException.ThrowIfNull(whenFailure);

        return Status switch
        {
            ResultStatus.Ok or ResultStatus.Created or ResultStatus.Accepted => whenSuccess(GetRequiredValue()),
            ResultStatus.Invalid or ResultStatus.Unauthorized or ResultStatus.Forbidden or ResultStatus.NotFound or ResultStatus.Conflict or ResultStatus.Error or ResultStatus.CriticalError or ResultStatus.Unavailable => whenFailure(GetRequiredError()),
            _ => throw new InvalidOperationException(UninitializedResultMessage),
        };
    }

    /// <summary>
    /// Converts this result to a non-generic result.
    /// </summary>
    /// <returns>A non-generic result preserving status and error information.</returns>
    public Result ToResult()
    {
        if (IsSuccess)
        {
            return new Result(Status, null);
        }

        if (IsFailure)
        {
            return new Result(Status, GetRequiredError());
        }

        throw new InvalidOperationException(UninitializedResultMessage);
    }

    private T GetRequiredValue() =>
        value ?? throw new InvalidOperationException(SuccessfulResultMustContainValueMessage);

    private ResultError GetRequiredError() =>
        error ?? throw new InvalidOperationException(FailedResultMustContainErrorDetailsMessage);

    /// <inheritdoc />
    public bool Equals(Result<T> other)
    {
        if (IsSuccess != other.IsSuccess || Status != other.Status)
        {
            return false;
        }

        return IsSuccess
            ? EqualityComparer<T>.Default.Equals(value, other.value)
            : EqualityComparer<ResultError?>.Default.Equals(error, other.error);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        IsSuccess
            ? HashCode.Combine(IsSuccess, Status, value)
            : HashCode.Combine(IsSuccess, Status, error);

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsSuccess)
        {
            return $"Success: {Status} - {value}";
        }

        if (IsFailure)
        {
            return $"Failure: {Status} - {error?.Detail}";
        }

        return $"Unknown: {Status}";
    }

    /// <summary>
    /// Determines whether two result instances are equal.
    /// </summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two result instances are not equal.
    /// </summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
}
