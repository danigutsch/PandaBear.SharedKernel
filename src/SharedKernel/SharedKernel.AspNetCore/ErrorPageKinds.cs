using Microsoft.AspNetCore.Http;

namespace SharedKernel.AspNetCore;

/// <summary>Names the <see cref="ErrorPageKind" /> of a failed status code, so an error page picks its text from the kind, not the number.</summary>
public static class ErrorPageKinds
{
    private const int LastServerError = 599;

    /// <summary>Gets the kind of a failed status code.</summary>
    /// <param name="statusCode">The status code of the failed response, from 400 to 599.</param>
    /// <returns>
    /// <see cref="ErrorPageKind.NotFound" /> for 404; <see cref="ErrorPageKind.Failure" /> for 408, 429 and every server error; otherwise
    /// <see cref="ErrorPageKind.RequestError" />.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="statusCode" /> is below 400 or above 599.</exception>
    public static ErrorPageKind Of(int statusCode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(statusCode, StatusCodes.Status400BadRequest);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(statusCode, LastServerError);
        return statusCode switch
        {
            StatusCodes.Status404NotFound => ErrorPageKind.NotFound,
            StatusCodes.Status408RequestTimeout or StatusCodes.Status429TooManyRequests => ErrorPageKind.Failure,
            < StatusCodes.Status500InternalServerError => ErrorPageKind.RequestError,
            _ => ErrorPageKind.Failure,
        };
    }
}
