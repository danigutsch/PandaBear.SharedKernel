using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace PandaBear.AspNetCore;

/// <summary>Answers requests for paths that moved with a permanent redirect to a local path.</summary>
public static partial class PermanentRedirectExtensions
{
    /// <summary>
    /// Asks <paramref name="resolve"/> where the path of each GET or HEAD request moved, and answers with a 301 to that local path. Other
    /// methods pass through without asking. The request continues unchanged when the resolver answers <see langword="null"/>, when the target
    /// is the request path itself in its percent-encoded form (loop guard), and when the target is not a plain local path; such a target is never redirected to, and a
    /// warning is logged without the target.
    /// </summary>
    /// <remarks>
    /// A plain local path starts with a single <c>/</c> and holds only printable ASCII without <c>\</c>, <c>//</c>, <c>?</c>, <c>#</c> or white
    /// space; encode other characters with percent escapes. That rules out schemes, protocol-relative and backslash addresses, header
    /// injection, and query or fragment smuggling, so the redirect cannot leave the site. The location is the request's percent-encoded path base, the target,
    /// and the request's query string when <see cref="PermanentRedirect.PreserveQuery"/> is set. Register it before the endpoints and before a
    /// redirect that needs the moved path to exist, such as a culture prefix redirect.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="resolve">
    /// Finds the target of the request path, or <see langword="null"/> when it did not move. Use the context's request services for data and
    /// <see cref="HttpContext.RequestAborted"/> for cancellation.
    /// </param>
    /// <returns>The application builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> or <paramref name="resolve"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UsePermanentRedirects(this IApplicationBuilder app, Func<HttpContext, ValueTask<PermanentRedirect?>> resolve)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(resolve);

        var logger = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(PermanentRedirectExtensions).FullName!);
        return app.Use(async (context, next) =>
        {
            var request = context.Request;
            if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) && await resolve(context).ConfigureAwait(false) is { } redirect)
            {
                if (!IsPlainLocalPath(redirect.Path))
                {
                    if (logger is not null)
                    {
                        LogUnsafeTarget(logger);
                    }
                }
                else if (!string.Equals(redirect.Path, request.Path.ToUriComponent(), StringComparison.Ordinal))
                {
                    var location = request.PathBase.ToUriComponent() + redirect.Path + (redirect.PreserveQuery ? request.QueryString.Value : string.Empty);
                    context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
                    context.Response.Headers[HeaderNames.Location] = location;
                    return;
                }
            }

            await next(context).ConfigureAwait(false);
        });
    }

    private static bool IsPlainLocalPath(string path)
    {
        if (path.Length == 0 || path[0] != '/' || path.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in path)
        {
            if (character is <= ' ' or >= '\u007F' or '\\' or '?' or '#')
            {
                return false;
            }
        }

        return true;
    }

    [LoggerMessage(EventId = 4004, EventName = "UnsafeRedirectTargetIgnored", Level = LogLevel.Warning, Message = "A permanent redirect target was ignored because it is not a plain local path.")]
    private static partial void LogUnsafeTarget(ILogger logger);
}
