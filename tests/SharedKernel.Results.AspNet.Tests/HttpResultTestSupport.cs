using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace PandaBear.Results.AspNet.Tests;

/// <summary>Builds failures that carry internal text and asserts that the text never reaches the client.</summary>
internal static class HttpResultTestSupport
{
    /// <summary>A recognizable internal detail that must never be sent to a client.</summary>
    internal const string Secret = "SECRET-sql-select-from-users-at-/srv/app";

    /// <summary>Creates a server-side failure whose detail is <see cref="Secret"/>.</summary>
    /// <param name="status">The server-side status.</param>
    /// <returns>The failed result.</returns>
    internal static Result FailureWithSecret(ResultStatus status) => status switch
    {
        ResultStatus.Error => Result.Error(Secret),
        ResultStatus.CriticalError => Result.CriticalError(Secret),
        _ => Result.Unavailable(Secret),
    };

    /// <summary>Creates a valued server-side failure whose detail is <see cref="Secret"/>.</summary>
    /// <param name="status">The server-side status.</param>
    /// <returns>The failed result.</returns>
    internal static Result<int> ValuedFailureWithSecret(ResultStatus status) => status switch
    {
        ResultStatus.Error => Result.Error<int>(Secret),
        ResultStatus.CriticalError => Result.CriticalError<int>(Secret),
        _ => Result.Unavailable<int>(Secret),
    };

    /// <summary>Asserts a problem response with the fixed detail and without <see cref="Secret"/> anywhere.</summary>
    /// <param name="http">The response.</param>
    /// <param name="expectedStatusCode">The expected status code.</param>
    /// <param name="expectedDetail">The expected fixed detail.</param>
    internal static void AssertGenericProblem(IResult http, int expectedStatusCode, string expectedDetail)
    {
        var problem = http.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(expectedStatusCode);
        problem.ProblemDetails.Detail.ShouldBe(expectedDetail);
        JsonSerializer.Serialize(problem.ProblemDetails).Contains(Secret, StringComparison.Ordinal).ShouldBeFalse();
    }
}
