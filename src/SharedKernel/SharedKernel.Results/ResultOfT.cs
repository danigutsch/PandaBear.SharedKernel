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
    public bool IsFailure => !IsSuccess;

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
            ? value
            : throw new InvalidOperationException($"Cannot access Value of a failed result. Error: {error?.Detail}");

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
    /// Converts this result to a non-generic result.
    /// </summary>
    /// <returns>A non-generic result preserving status and error information.</returns>
    public Result ToResult() => new(Status, ErrorDetails);

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

    /// <summary>
    /// Determines whether two result instances are equal.
    /// </summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two result instances are not equal.
    /// </summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
}
