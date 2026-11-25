using Microsoft.AspNetCore.Http;

namespace SharedKernel.Results.AspNet;

/// <summary>
/// Provides ASP.NET Core HTTP mappings for result statuses.
/// </summary>
public static class ResultStatusHttpExtensions
{
    /// <summary>
    /// Converts a result status to its conventional HTTP status code.
    /// </summary>
    /// <param name="status">The result status to convert.</param>
    /// <returns>The conventional HTTP status code for the result status.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The status is not a defined result status.</exception>
    public static int ToHttpStatusCode(this ResultStatus status)
    {
        return status switch
        {
            ResultStatus.Ok => StatusCodes.Status200OK,
            ResultStatus.Created => StatusCodes.Status201Created,
            ResultStatus.Accepted => StatusCodes.Status202Accepted,
            ResultStatus.NoContent => StatusCodes.Status204NoContent,
            ResultStatus.Invalid => StatusCodes.Status400BadRequest,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            ResultStatus.Error => StatusCodes.Status500InternalServerError,
            ResultStatus.CriticalError => StatusCodes.Status500InternalServerError,
            ResultStatus.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The result status does not have a conventional HTTP status code."),
        };
    }
}
