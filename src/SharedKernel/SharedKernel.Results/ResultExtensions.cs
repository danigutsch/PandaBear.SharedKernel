namespace SharedKernel.Results;

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
    /// <returns>A failed generic result with the same error information.</returns>
    public static Result<TTarget> ConvertError<TTarget>(this Result source)
        where TTarget : notnull =>
        ConvertFailureResult(source.Status, source.ErrorDetails, CreateGenericFactories<TTarget>());

    /// <summary>
    /// Converts a failed generic result to a non-generic result.
    /// </summary>
    /// <typeparam name="TSource">The source success value type.</typeparam>
    /// <param name="source">The source failed result.</param>
    /// <returns>A failed non-generic result with the same error information.</returns>
    public static Result ConvertError<TSource>(this Result<TSource> source)
        where TSource : notnull =>
        ConvertFailureResult(source.Status, source.ErrorDetails, NonGenericFactories.Instance);

    /// <summary>
    /// Converts a failed generic result to another generic result type.
    /// </summary>
    /// <typeparam name="TSource">The source success value type.</typeparam>
    /// <typeparam name="TTarget">The target success value type.</typeparam>
    /// <param name="source">The source failed result.</param>
    /// <returns>A failed generic result with the same error information.</returns>
    public static Result<TTarget> ConvertError<TSource, TTarget>(this Result<TSource> source)
        where TSource : notnull
        where TTarget : notnull =>
        ConvertFailureResult(source.Status, source.ErrorDetails, CreateGenericFactories<TTarget>());

    private static FailureResultFactories<Result<TTarget>> CreateGenericFactories<TTarget>()
        where TTarget : notnull =>
        new(
            Result.Invalid<TTarget>,
            Result.NotFound<TTarget>,
            Result.Unauthorized<TTarget>,
            Result.Forbidden<TTarget>,
            Result.Error<TTarget>,
            Result.Conflict<TTarget>,
            Result.CriticalError<TTarget>,
            Result.Unavailable<TTarget>);

    private static TResult ConvertFailureResult<TResult>(
        ResultStatus status,
        ResultError? errorDetails,
        FailureResultFactories<TResult> factories)
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

        return status switch
        {
            ResultStatus.Invalid => factories.Invalid(details.Detail, ToValidationDictionary(details.ValidationErrors)),
            ResultStatus.NotFound => factories.NotFound(details.Detail),
            ResultStatus.Unauthorized => factories.Unauthorized(details.Detail),
            ResultStatus.Forbidden => factories.Forbidden(details.Detail),
            ResultStatus.Error => factories.Error(details.Detail),
            ResultStatus.Conflict => factories.Conflict(details.Detail),
            ResultStatus.CriticalError => factories.CriticalError(details.Detail),
            ResultStatus.Unavailable => factories.Unavailable(details.Detail),
            _ => throw new InvalidOperationException($"Unsupported result status: {status}"),
        };
    }

    private static Dictionary<string, string[]> ToValidationDictionary(IReadOnlyDictionary<string, IReadOnlyList<string>>? validationErrors)
    {
        if (validationErrors is null)
        {
            throw new InvalidOperationException(FailedResultMustContainErrorDetailsMessage);
        }

        var result = new Dictionary<string, string[]>(validationErrors.Count, StringComparer.Ordinal);
        foreach (var (field, messages) in validationErrors)
        {
            result[field] = [.. messages];
        }

        return result;
    }

    private readonly record struct FailureResultFactories<TResult>(
        Func<string, Dictionary<string, string[]>, TResult> Invalid,
        Func<string, TResult> NotFound,
        Func<string, TResult> Unauthorized,
        Func<string, TResult> Forbidden,
        Func<string, TResult> Error,
        Func<string, TResult> Conflict,
        Func<string, TResult> CriticalError,
        Func<string, TResult> Unavailable);

    private static class NonGenericFactories
    {
        internal static FailureResultFactories<Result> Instance { get; } =
            new(
                Result.Invalid,
                Result.NotFound,
                Result.Unauthorized,
                Result.Forbidden,
                Result.Error,
                Result.Conflict,
                Result.CriticalError,
                Result.Unavailable);
    }
}
