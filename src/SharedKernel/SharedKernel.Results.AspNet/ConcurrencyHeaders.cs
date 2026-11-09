using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace PandaBear.Results.AspNet;

/// <summary>
/// Carries a record's numeric concurrency version over HTTP: <c>ETag</c> on reads, <c>If-Match</c> on updates.
/// </summary>
public static class ConcurrencyHeaders
{
    private const string VersionField = "version";

    private const string IfMatchDetail = "The If-Match header must carry the version that was read.";

    private const string IfMatchHint = "Send the strong ETag exactly as you read it (for example \"3\") in the If-Match header.";

    /// <summary>Formats a version as a strong entity tag.</summary>
    /// <param name="version">The record's version.</param>
    /// <returns>The quoted entity tag, for example <c>"3"</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is negative.</exception>
    public static string ETag(long version)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        return $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";
    }

    /// <summary>Reads the version an update is based on from the <c>If-Match</c> header.</summary>
    /// <remarks>
    /// RFC 9110 requires strong comparison for <c>If-Match</c>, so only exactly one well-formed strong tag holding a non-negative integer in
    /// the canonical form <see cref="ETag(long)"/> produces is accepted (no leading zeros, no sign). A missing or unusable header is
    /// deliberately a 400 validation problem keyed <c>version</c>, not 428 Precondition Required. A stale version is not detected here: it is
    /// whatever the operation returns (409 Conflict is recommended, not 412). <see cref="Update"/> returns no new <c>ETag</c>; the client reads again.
    /// </remarks>
    /// <param name="request">The request.</param>
    /// <returns>
    /// The version, or an invalid result keyed to <c>version</c> when the header is missing, is <c>*</c>, a weak tag (<c>W/"n"</c>),
    /// a list of tags, malformed, or not a canonical version.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public static Result<long> ReadVersion(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The strict parser rejects the whole header when any list element is malformed; the typed-header parser would silently drop it.
        return EntityTagHeaderValue.TryParseStrictList(request.Headers.IfMatch, out var tags)
            && tags is [{ IsWeak: false } tag]
            && long.TryParse(tag.Tag.Value?.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            && tag.Tag.Value == ETag(version)
            ? Result.Ok(version)
            : Result.Invalid<long>(IfMatchDetail, VersionField, IfMatchHint);
    }

    /// <summary>Reads the <c>If-Match</c> version, runs the update with it, and converts the outcome to a response.</summary>
    /// <param name="request">The request.</param>
    /// <param name="send">The update; it does not run when the header is unusable.</param>
    /// <returns>The validation problem for an unusable header, otherwise the converted result of <paramref name="send"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="send"/> is <see langword="null"/>.</exception>
    public static Task<IResult> Update(HttpRequest request, Func<long, ValueTask<Result>> send)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(send);

        return UpdateCore(request, send);
    }

    private static async Task<IResult> UpdateCore(HttpRequest request, Func<long, ValueTask<Result>> send)
    {
        var version = ReadVersion(request);
        return version.IsFailure
            ? version.ToHttpResult()
            : (await send(version.Value).ConfigureAwait(false)).ToHttpResult();
    }
}
