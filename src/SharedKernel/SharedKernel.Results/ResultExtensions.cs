namespace PandaBear.Results;

/// <summary>
/// Extension methods for converting failed results.
/// </summary>
public static class ResultExtensions
{
    private const string CannotConvertSuccessfulResultMessage = "Cannot convert a successful result. Only failed results can be converted.";
    private const string FailedResultMustContainErrorDetailsMessage = "Failed results must contain error details.";

    /// <summary>
    /// Converts a failed non-generic result to a generic result.
    /// </summary>
    /// <typeparam name="TTarget">The target success value type.</typeparam>
    /// <param name="source">The source failed result.</param>
    /// <returns>A failed generic result with the same status and the same error, including its code.</returns>
    public static Result<TTarget> ConvertError<TTarget>(this Result source)
        where TTarget : notnull
    {
        var status = source.Status;
        var error = RequireConvertibleFailure(status, source.ErrorDetails);
        return new Result<TTarget>(status, default, error);
    }

    /// <summary>
    /// Converts a failed generic result to a non-generic result.
    /// </summary>
    /// <typeparam name="TSource">The source success value type.</typeparam>
    /// <param name="source">The source failed result.</param>
    /// <returns>A failed non-generic result with the same status and the same error, including its code.</returns>
    public static Result ConvertError<TSource>(this Result<TSource> source)
        where TSource : notnull
    {
        var status = source.Status;
        var error = RequireConvertibleFailure(status, source.ErrorDetails);
        return new Result(status, error);
    }

    /// <summary>
    /// Converts a failed generic result to another generic result type.
    /// </summary>
    /// <typeparam name="TSource">The source success value type.</typeparam>
    /// <typeparam name="TTarget">The target success value type.</typeparam>
    /// <param name="source">The source failed result.</param>
    /// <returns>A failed generic result with the same status and the same error, including its code.</returns>
    public static Result<TTarget> ConvertError<TSource, TTarget>(this Result<TSource> source)
        where TSource : notnull
        where TTarget : notnull
    {
        var status = source.Status;
        var error = RequireConvertibleFailure(status, source.ErrorDetails);
        return new Result<TTarget>(status, default, error);
    }

    private static ResultError RequireConvertibleFailure(ResultStatus status, ResultError? errorDetails)
    {
        if (status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.Accepted or ResultStatus.NoContent)
        {
            throw new InvalidOperationException(CannotConvertSuccessfulResultMessage);
        }

        if (status is ResultStatus.Unknown || !Enum.IsDefined(status))
        {
            throw new InvalidOperationException($"Unsupported result status: {status}");
        }

        var details = errorDetails ?? throw new InvalidOperationException(FailedResultMustContainErrorDetailsMessage);
        if (status is ResultStatus.Invalid && (details.ValidationErrors is null || details.ValidationErrors.Count == 0))
        {
            throw new InvalidOperationException(ResultInvariantMessages.ValidationErrorsMustContainFieldDetails);
        }

        return details;
    }
}
