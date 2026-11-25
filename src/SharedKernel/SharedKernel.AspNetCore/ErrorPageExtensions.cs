using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace SharedKernel.AspNetCore;

/// <summary>Error pages for a site whose failed requests are answered by one page per status code.</summary>
public static class ErrorPageExtensions
{
    /// <summary>
    /// Adds the error handling of a site: an unhandled exception re-executes the request as <c>{prefix}/500</c> and an empty error
    /// response as <c>{prefix}/{status}</c>, keeping the original status code.
    /// <list type="bullet">
    /// <item>Each re-execution gets a new service scope, because the page that failed may already have used the request's scope.</item>
    /// <item>
    /// The page runs as a GET, so a re-executed POST is not read as a form submission; a HEAD stays a HEAD. Routing still matches the
    /// re-executed request with its original method, so the page's endpoint must accept every method.
    /// </item>
    /// <item>
    /// When request localization ran before, the page sets <c>Content-Language</c> to the request's language again, because the exception
    /// handler clears the headers.
    /// </item>
    /// <item>The pages answer only a re-execution: a direct request under <paramref name="prefix" /> or <paramref name="hiddenPaths" /> is not found.</item>
    /// </list>
    /// Call it after <c>UseRequestLocalization</c>, so the page is in the language of the requested path.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="prefix">The path below which the pages live, such as <c>/error</c>.</param>
    /// <param name="hiddenPaths">Other paths that only a re-execution may reach, such as the <c>/not-found</c> page a Blazor router needs.</param>
    /// <returns>The same application builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app" /> or <paramref name="hiddenPaths" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="prefix" /> or one of <paramref name="hiddenPaths" /> is empty or <c>/</c>.</exception>
    public static IApplicationBuilder UseErrorPages(this IApplicationBuilder app, PathString prefix, params PathString[] hiddenPaths)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(hiddenPaths);
        PathString[] hidden = [prefix, .. hiddenPaths];
        if (hidden.Any(static path => !path.HasValue || path.Value == "/"))
        {
            throw new ArgumentException("Name the error pages and hidden paths below the root, such as '/error'.", nameof(prefix));
        }

        app.UseExceptionHandler(prefix.Value + "/500", createScopeForErrors: true);
        app.UseStatusCodePagesWithReExecute(prefix.Value + "/{0}", createScopeForStatusCodePages: true);
        return app.Use((context, next) =>
        {
            if (!IsReExecution(context))
            {
                return IsHidden(context.Request.Path, hidden) ? NotFound(context) : next(context);
            }

            if (!HttpMethods.IsHead(context.Request.Method))
            {
                context.Request.Method = HttpMethods.Get;
            }

            if (context.Features.Get<IRequestCultureFeature>() is { } culture)
            {
                context.Response.Headers.ContentLanguage = culture.RequestCulture.UICulture.Name;
            }

            return next(context);
        });
    }

    private static bool IsReExecution(HttpContext context) =>
        context.Features.Get<IStatusCodeReExecuteFeature>() is not null || context.Features.Get<IExceptionHandlerFeature>() is not null;

    private static bool IsHidden(PathString path, PathString[] hidden) =>
        hidden.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static Task NotFound(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}
