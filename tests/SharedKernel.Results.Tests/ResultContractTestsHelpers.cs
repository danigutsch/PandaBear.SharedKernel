namespace PandaBear.Results.Tests;

/// <summary>
/// Generic helpers written against <see cref="IResultLike{TSelf}"/> for the result contract tests.
/// </summary>
internal static class ResultContractTestsHelpers
{
    /// <summary>Describes a result through the contract only.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="result">The result to describe.</param>
    public static string Describe<TResult>(TResult result)
        where TResult : IResultLike<TResult> =>
        result.IsFailure ? $"failure:{result.ErrorDetails!.Detail}" : "success";

    /// <summary>Builds an invalid result through the contract.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    public static TResult InvalidWithField<TResult>()
        where TResult : IResultLike<TResult> => TResult.Invalid("bad", "name", "required");

    /// <summary>Builds an invalid result from a dictionary through the contract.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="errors">The messages per field.</param>
    public static TResult InvalidFromDictionary<TResult>(IReadOnlyDictionary<string, string[]> errors)
        where TResult : IResultLike<TResult> => TResult.Invalid("bad", errors);

    /// <summary>Builds a not-found result through the contract.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="detail">The failure detail.</param>
    public static TResult NotFound<TResult>(string detail)
        where TResult : IResultLike<TResult> => TResult.NotFound(detail);

    /// <summary>Builds a conflict result through the contract.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="detail">The failure detail.</param>
    public static TResult Conflict<TResult>(string detail)
        where TResult : IResultLike<TResult> => TResult.Conflict(detail);

    /// <summary>The statuses expected from <see cref="FactoryStatuses{TResult}"/>.</summary>
    public static readonly ResultStatus[] ExpectedFactoryStatuses =
    [
        ResultStatus.Invalid,
        ResultStatus.NotFound,
        ResultStatus.Unauthorized,
        ResultStatus.Forbidden,
        ResultStatus.Conflict,
        ResultStatus.Error,
        ResultStatus.CriticalError,
        ResultStatus.Unavailable,
    ];

    /// <summary>Builds one result per factory on the contract and returns their statuses.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    public static ResultStatus[] FactoryStatuses<TResult>()
        where TResult : IResultLike<TResult> =>
    [
        TResult.Invalid("d", "f", "m").Status,
        TResult.NotFound("d").Status,
        TResult.Unauthorized("d").Status,
        TResult.Forbidden("d").Status,
        TResult.Conflict("d").Status,
        TResult.Error("d").Status,
        TResult.CriticalError("d").Status,
        TResult.Unavailable("d").Status,
    ];

    /// <summary>Describes the default instance of the result type through the contract.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    public static string DefaultState<TResult>()
        where TResult : struct, IResultLike<TResult>
    {
        var result = default(TResult);
        return $"{result.Status}:{result.IsSuccess}:{result.IsFailure}";
    }

    /// <summary>An open-generic validation behavior shape that short-circuits with an invalid result.</summary>
    /// <typeparam name="TResponse">The result type.</typeparam>
    public sealed class ValidationBehavior<TResponse>
        where TResponse : struct, IResultLike<TResponse>
    {
        /// <summary>Returns an invalid result for a blank name; otherwise calls next.</summary>
        /// <param name="name">The name to validate.</param>
        /// <param name="next">The continuation.</param>
        public static ValueTask<TResponse> Run(string name, Func<ValueTask<TResponse>> next) =>
            string.IsNullOrWhiteSpace(name)
                ? ValueTask.FromResult(TResponse.Invalid("Validation failed", "name", "Name is required"))
                : next();
    }
}
