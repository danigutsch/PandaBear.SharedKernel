using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace PandaBear.Results.AspNet;

/// <summary>Converts results to minimal API responses.</summary>
public static class ResultHttpExtensions
{
    /// <summary>The fixed problem detail sent for failures that map to HTTP 500; the result's own detail is never sent.</summary>
    public const string InternalServerErrorDetail = "An unexpected error occurred.";

    /// <summary>The fixed problem detail sent for failures that map to HTTP 503; the result's own detail is never sent.</summary>
    public const string ServiceUnavailableDetail = "The service is temporarily unavailable.";

    private const string UninitializedResultMessage = "Result status is not initialized.";

    /// <summary>Converts a result to an HTTP response with no body on success.</summary>
    /// <remarks>
    /// A failure with a server-side status (HTTP 5xx) never sends the result's own detail to the client; a fixed generic detail is sent instead.
    /// </remarks>
    /// <param name="result">The result.</param>
    /// <returns>204 for Ok and NoContent, 202 for Accepted, otherwise a problem response.</returns>
    /// <exception cref="InvalidOperationException">The result is not initialized.</exception>
    public static IResult ToHttpResult(this Result result)
    {
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return result.Status switch
        {
            ResultStatus.Accepted => HttpResults.Accepted(),
            _ => HttpResults.NoContent(),
        };
    }

    /// <summary>Converts a result to an HTTP response with the value as the body on success.</summary>
    /// <remarks>
    /// A Created result becomes 201 with the value as body and no Location header. A failure with a server-side status (HTTP 5xx)
    /// never sends the result's own detail to the client; a fixed generic detail is sent instead.
    /// </remarks>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>200, 201 or 202 with the value on success, otherwise a problem response.</returns>
    /// <exception cref="InvalidOperationException">The result is not initialized.</exception>
    public static IResult ToHttpResult<T>(this Result<T> result)
        where T : notnull
    {
        if (!result.IsSuccess)
        {
            return Failure(result.IsFailure ? result.WithoutValue() : default);
        }

        return result.Status switch
        {
            ResultStatus.Created => HttpResults.Created((Uri?)null, result.Value),
            ResultStatus.Accepted => HttpResults.Accepted((string?)null, result.Value),
            _ => HttpResults.Ok(result.Value),
        };
    }

    private static IResult Failure(Result result)
    {
        if (!result.IsFailure)
        {
            throw new InvalidOperationException(UninitializedResultMessage);
        }

        var error = result.ErrorDetails;
        if (result.Status == ResultStatus.Invalid)
        {
            return HttpResults.ValidationProblem(result.ValidationErrors(), error.Detail);
        }

        var statusCode = result.Status.ToHttpStatusCode();
        return HttpResults.Problem(detail: PublicDetail(statusCode, error.Detail), statusCode: statusCode);
    }

    private static string PublicDetail(int statusCode, string callerDetail) => statusCode switch
    {
        StatusCodes.Status503ServiceUnavailable => ServiceUnavailableDetail,
        >= StatusCodes.Status500InternalServerError => InternalServerErrorDetail,
        _ => callerDetail,
    };
}
