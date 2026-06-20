using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Results;

/// <summary>
/// Represents the result of an operation that can succeed with a value or fail with an error.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Compatibility factories preserve the established Result<T> call shape during migration to SharedKernel.Functional.")]
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
    /// Maps the current success value into a new result value using an asynchronous projection.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null success type.</typeparam>
    /// <param name="map">The asynchronous projection to apply when the result succeeded.</param>
    /// <returns>The mapped result, or the original failure when the result failed.</returns>
    public async Task<Result<TResult>> Map<TResult>(Func<T, Task<TResult>> map)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        if (TryGetValue(out var currentValue))
        {
            return Result.Ok(await map(currentValue).ConfigureAwait(false));
        }

        return new Result<TResult>(Status, default, ErrorDetails);
    }

    /// <summary>
    /// Maps the current success value into a new result value using an asynchronous projection.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null success type.</typeparam>
    /// <param name="map">The asynchronous projection to apply when the result succeeded.</param>
    /// <returns>The mapped result, or the original failure when the result failed.</returns>
    public async ValueTask<Result<TResult>> Map<TResult>(Func<T, ValueTask<TResult>> map)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        if (TryGetValue(out var currentValue))
        {
            return Result.Ok(await map(currentValue).ConfigureAwait(false));
        }

        return new Result<TResult>(Status, default, ErrorDetails);
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
    /// Binds the current success value into another result using an asynchronous projection.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null success type.</typeparam>
    /// <param name="bind">The asynchronous projection to apply when the result succeeded.</param>
    /// <returns>The bound result, or the original failure when the result failed.</returns>
    public async Task<Result<TResult>> Bind<TResult>(Func<T, Task<Result<TResult>>> bind)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        if (TryGetValue(out var currentValue))
        {
            return await bind(currentValue).ConfigureAwait(false);
        }

        return new Result<TResult>(Status, default, ErrorDetails);
    }

    /// <summary>
    /// Binds the current success value into another result using an asynchronous projection.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null success type.</typeparam>
    /// <param name="bind">The asynchronous projection to apply when the result succeeded.</param>
    /// <returns>The bound result, or the original failure when the result failed.</returns>
    public async ValueTask<Result<TResult>> Bind<TResult>(Func<T, ValueTask<Result<TResult>>> bind)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        if (TryGetValue(out var currentValue))
        {
            return await bind(currentValue).ConfigureAwait(false);
        }

        return new Result<TResult>(Status, default, ErrorDetails);
    }

    /// <summary>
    /// Ensures the current success value satisfies the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate to evaluate when the result succeeded.</param>
    /// <param name="error">The error to return when the predicate fails.</param>
    /// <returns>The original result when the predicate succeeds, or the provided failure when it fails.</returns>
    public Result<T> Ensure(Func<T, bool> predicate, ResultError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (TryGetValue(out var currentValue))
        {
            return predicate(currentValue)
                ? this
                : CreateEnsureFailureResult(error);
        }

        return this;
    }

    /// <summary>
    /// Ensures the current success value satisfies the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate to evaluate when the result succeeded.</param>
    /// <param name="error">The error to return when the predicate fails.</param>
    /// <returns>The original result when the predicate succeeds, or the provided failure when it fails.</returns>
    public async Task<Result<T>> Ensure(Func<T, Task<bool>> predicate, ResultError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (TryGetValue(out var currentValue))
        {
            return await predicate(currentValue).ConfigureAwait(false)
                ? this
                : CreateEnsureFailureResult(error);
        }

        return this;
    }

    /// <summary>
    /// Ensures the current success value satisfies the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate to evaluate when the result succeeded.</param>
    /// <param name="error">The error to return when the predicate fails.</param>
    /// <returns>The original result when the predicate succeeds, or the provided failure when it fails.</returns>
    public async ValueTask<Result<T>> Ensure(Func<T, ValueTask<bool>> predicate, ResultError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (TryGetValue(out var currentValue))
        {
            return await predicate(currentValue).ConfigureAwait(false)
                ? this
                : CreateEnsureFailureResult(error);
        }

        return this;
    }

    private static Result<T> CreateEnsureFailureResult(ResultError error)
    {
        return new Result<T>(GetEnsureFailureStatus(error), default, error);
    }

    private static ResultStatus GetEnsureFailureStatus(ResultError error)
    {
        return error.Code switch
        {
            ResultErrorCodes.Invalid => ResultStatus.Invalid,
            ResultErrorCodes.NotFound => ResultStatus.NotFound,
            ResultErrorCodes.Unauthorized => ResultStatus.Unauthorized,
            ResultErrorCodes.Forbidden => ResultStatus.Forbidden,
            ResultErrorCodes.Conflict => ResultStatus.Conflict,
            ResultErrorCodes.CriticalError => ResultStatus.CriticalError,
            ResultErrorCodes.Unavailable => ResultStatus.Unavailable,
            _ => ResultStatus.Error,
        };
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
    /// Projects the result into one of two result values using asynchronous delegates.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="whenSuccess">Called when the result succeeded.</param>
    /// <param name="whenFailure">Called when the result failed.</param>
    /// <returns>The value produced by the matched branch.</returns>
    public async Task<TResult> Match<TResult>(Func<T, Task<TResult>> whenSuccess, Func<ResultError, Task<TResult>> whenFailure)
    {
        ArgumentNullException.ThrowIfNull(whenSuccess);
        ArgumentNullException.ThrowIfNull(whenFailure);

        return Status switch
        {
            ResultStatus.Ok or ResultStatus.Created or ResultStatus.Accepted => await whenSuccess(GetRequiredValue()).ConfigureAwait(false),
            ResultStatus.Invalid or ResultStatus.Unauthorized or ResultStatus.Forbidden or ResultStatus.NotFound or ResultStatus.Conflict or ResultStatus.Error or ResultStatus.CriticalError or ResultStatus.Unavailable => await whenFailure(GetRequiredError()).ConfigureAwait(false),
            _ => throw new InvalidOperationException(UninitializedResultMessage),
        };
    }

    /// <summary>
    /// Projects the result into one of two result values using asynchronous delegates.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="whenSuccess">Called when the result succeeded.</param>
    /// <param name="whenFailure">Called when the result failed.</param>
    /// <returns>The value produced by the matched branch.</returns>
    public async ValueTask<TResult> Match<TResult>(Func<T, ValueTask<TResult>> whenSuccess, Func<ResultError, ValueTask<TResult>> whenFailure)
    {
        ArgumentNullException.ThrowIfNull(whenSuccess);
        ArgumentNullException.ThrowIfNull(whenFailure);

        return Status switch
        {
            ResultStatus.Ok or ResultStatus.Created or ResultStatus.Accepted => await whenSuccess(GetRequiredValue()).ConfigureAwait(false),
            ResultStatus.Invalid or ResultStatus.Unauthorized or ResultStatus.Forbidden or ResultStatus.NotFound or ResultStatus.Conflict or ResultStatus.Error or ResultStatus.CriticalError or ResultStatus.Unavailable => await whenFailure(GetRequiredError()).ConfigureAwait(false),
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
