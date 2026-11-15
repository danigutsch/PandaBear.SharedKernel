using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace PandaBear.AspNetCore;

/// <summary>Status-code pages that leave the answers under some path prefixes, such as an API, as the endpoints wrote them.</summary>
public static class StatusCodePagesExclusionExtensions
{
    /// <summary>
    /// Adds <see cref="StatusCodePagesExtensions.UseStatusCodePagesWithReExecute(IApplicationBuilder, string, string?, bool)"/> and turns
    /// it off for requests whose path starts with one of <paramref name="excludedPrefixes"/> (whole segments, ignoring case, relative to the
    /// path base), so for example <c>/api</c> keeps its empty or problem answers while every other empty error response is re-executed as
    /// the page. Call it on the application itself, not inside a <c>UseWhen</c> branch: a branch has no global route builder, so the
    /// re-executed request would not find the page's endpoint.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="pathFormat">The path of the page, which may contain <c>{0}</c> for the status code, such as <c>/error/{0}</c>.</param>
    /// <param name="createScopeForStatusCodePages">Whether the page runs in a new service scope instead of the scope of the failed request.</param>
    /// <param name="excludedPrefixes">The path prefixes whose answers are never re-executed, such as <c>/api</c>.</param>
    /// <returns>The same application builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> or <paramref name="excludedPrefixes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="pathFormat"/> is blank, or <paramref name="excludedPrefixes"/> is empty or holds an empty prefix or <c>/</c>.
    /// </exception>
    public static IApplicationBuilder UseStatusCodePagesWithReExecuteExcept(
        this IApplicationBuilder app,
        string pathFormat,
        bool createScopeForStatusCodePages,
        params PathString[] excludedPrefixes)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathFormat);
        ArgumentNullException.ThrowIfNull(excludedPrefixes);
        if (excludedPrefixes.Length == 0 || excludedPrefixes.Any(static prefix => !prefix.HasValue || prefix.Value == "/"))
        {
            throw new ArgumentException("Name at least one path prefix below the root, such as '/api'.", nameof(excludedPrefixes));
        }

        PathString[] prefixes = [.. excludedPrefixes];
        app.UseStatusCodePagesWithReExecute(pathFormat, createScopeForStatusCodePages: createScopeForStatusCodePages);
        return app.Use((context, next) =>
        {
            if (IsExcluded(context.Request.Path, prefixes) && context.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
            {
                statusCodePages.Enabled = false;
            }

            return next(context);
        });
    }

    private static bool IsExcluded(PathString path, PathString[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
