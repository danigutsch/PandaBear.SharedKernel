using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>Builds failed results for a status.</summary>
internal static class FailedResults
{
    /// <summary>Creates a failed result with the status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The result.</returns>
    public static Result For(ResultStatus status) => status switch
    {
        ResultStatus.Conflict => Result.Conflict("x"),
        ResultStatus.NotFound => Result.NotFound("x"),
        _ => Result.Error("x"),
    };
}
