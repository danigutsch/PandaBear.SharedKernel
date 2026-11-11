using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace PandaBear.HttpCaching.AspNetCore;

/// <summary>
/// Problem details for conditional writes: 428 Precondition Required when <c>If-Match</c> is missing, 400 when it is unusable, and 412
/// Precondition Failed when it does not match the current representation.
/// </summary>
public static class PreconditionProblems
{
    /// <summary>The default detail of <see cref="Required"/>.</summary>
    public const string RequiredDetail = "Send If-Match with the entity tag from the latest read.";

    /// <summary>The default detail of <see cref="InvalidIfMatch"/>.</summary>
    public const string InvalidIfMatchDetail = "If-Match must be \"*\" or a list of entity tags.";

    /// <summary>The default detail of <see cref="Failed"/>.</summary>
    public const string FailedDetail = "The resource changed after it was read. Read it again and reapply the changes.";

    /// <summary>Creates the 428 Precondition Required problem for a write sent without <c>If-Match</c>.</summary>
    /// <param name="detail">The detail; <see cref="RequiredDetail"/> when <see langword="null"/>.</param>
    /// <param name="extensions">Extension members, such as an application error code, or <see langword="null"/>.</param>
    /// <returns>The problem result.</returns>
    public static ProblemHttpResult Required(string? detail = null, IEnumerable<KeyValuePair<string, object?>>? extensions = null) =>
        Create(StatusCodes.Status428PreconditionRequired, detail ?? RequiredDetail, extensions);

    /// <summary>Creates the 400 Bad Request problem for an <c>If-Match</c> header that is not <c>*</c> or a list of entity tags.</summary>
    /// <param name="detail">The detail; <see cref="InvalidIfMatchDetail"/> when <see langword="null"/>.</param>
    /// <param name="extensions">Extension members, such as an application error code, or <see langword="null"/>.</param>
    /// <returns>The problem result.</returns>
    public static ProblemHttpResult InvalidIfMatch(string? detail = null, IEnumerable<KeyValuePair<string, object?>>? extensions = null) =>
        Create(StatusCodes.Status400BadRequest, detail ?? InvalidIfMatchDetail, extensions);

    /// <summary>Creates the 412 Precondition Failed problem for an <c>If-Match</c> that does not match the current representation.</summary>
    /// <param name="detail">The detail; <see cref="FailedDetail"/> when <see langword="null"/>.</param>
    /// <param name="extensions">Extension members, such as an application error code, or <see langword="null"/>.</param>
    /// <returns>The problem result.</returns>
    public static ProblemHttpResult Failed(string? detail = null, IEnumerable<KeyValuePair<string, object?>>? extensions = null) =>
        Create(StatusCodes.Status412PreconditionFailed, detail ?? FailedDetail, extensions);

    private static ProblemHttpResult Create(int statusCode, string detail, IEnumerable<KeyValuePair<string, object?>>? extensions) =>
        TypedResults.Problem(detail, statusCode: statusCode, extensions: extensions);
}
