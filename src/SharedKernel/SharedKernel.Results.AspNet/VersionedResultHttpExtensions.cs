using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace PandaBear.Results.AspNet;

/// <summary>Converts results to responses that carry a record version or a created location.</summary>
public static class VersionedResultHttpExtensions
{
    /// <summary>Converts a result like <see cref="ResultHttpExtensions.ToHttpResult{T}(Result{T})"/> and, on success, sets the version as the <c>ETag</c> header.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="version">Selects the record version from the value.</param>
    /// <returns>The converted response; only a successful one carries the entity tag.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The result is not initialized.</exception>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, long> version)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(version);

        var http = result.ToHttpResult();
        return result.IsSuccess ? new ETagHttpResult(http, ConcurrencyHeaders.ETag(version(result.Value))) : http;
    }

    /// <summary>Converts a successful result to 201 Created with a <c>Location</c> header; a failure becomes the usual problem response.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <typeparam name="TBody">The response body type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="location">Builds the location of the created resource from the value.</param>
    /// <param name="body">Builds the response body from the value.</param>
    /// <returns>201 with the location and body on success, otherwise a problem response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> or <paramref name="body"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The result is not initialized.</exception>
    public static IResult ToCreatedHttpResult<TValue, TBody>(this Result<TValue> result, Func<TValue, string> location, Func<TValue, TBody> body)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(body);

        return result.IsSuccess
            ? Microsoft.AspNetCore.Http.Results.Created(location(result.Value), body(result.Value))
            : result.ToHttpResult();
    }

    private sealed class ETagHttpResult(IResult inner, string tag) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            httpContext.Response.Headers[HeaderNames.ETag] = tag;
            return inner.ExecuteAsync(httpContext);
        }
    }
}
