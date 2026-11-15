using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Tests;

/// <summary>
/// Starts hosts whose status-code pages re-execute <c>/status/{code}</c> except under <c>/api</c> and <c>/health</c>. The page answers
/// <c>page {code} same scope</c> or <c>page {code} new scope</c>, depending on whether it saw the scoped service of the failed request.
/// </summary>
internal static class StatusCodePagesTestHost
{
    /// <summary>Starts a host.</summary>
    /// <param name="createScope">Whether the page runs in a new service scope.</param>
    /// <returns>The host.</returns>
    internal static Task<PipelineTestHost> Start(bool createScope) =>
        PipelineTestHost.Start(
            static services => services.AddScoped<StatusCodePageScope>().AddProblemDetails(),
            app =>
            {
                app.UseStatusCodePagesWithReExecuteExcept("/status/{0}", createScope, "/api", "/health");
                app.MapGet("/status/{code:int}", static (int code, HttpContext context, StatusCodePageScope scope) =>
                    $"page {code} {(ReferenceEquals(context.Items[nameof(StatusCodePageScope)], scope) ? "same" : "new")} scope");
                app.MapGet("/items", NotFoundRemembering);
                app.MapGet("/apiary/items", NotFoundRemembering);
                app.MapGet("/api/items", NotFoundRemembering);
                app.MapGet("/api/conflict", static () => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict));
            });

    private static NotFound NotFoundRemembering(HttpContext context, StatusCodePageScope scope)
    {
        context.Items[nameof(StatusCodePageScope)] = scope;
        return TypedResults.NotFound();
    }
}
