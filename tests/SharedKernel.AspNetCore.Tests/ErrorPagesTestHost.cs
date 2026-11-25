using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>
/// Starts hosts with error pages under <c>/error</c> and the hidden router page <c>/not-found</c>. The page answers
/// <c>page {code} same scope</c> or <c>page {code} new scope</c>, depending on whether it saw the scoped service of the failed request,
/// and names the method it ran with in <c>X-Page-Method</c>. <c>/fail</c> throws and <c>/missing</c> answers an empty 404, for GET, HEAD
/// and POST.
/// </summary>
internal static class ErrorPagesTestHost
{
    /// <summary>The header in which the page names the method it ran with.</summary>
    internal const string MethodHeader = "X-Page-Method";

    private static readonly string[] Methods = [HttpMethods.Get, HttpMethods.Head, HttpMethods.Post];

    /// <summary>Starts a host.</summary>
    /// <param name="localized">Whether request localization (<c>pt-BR</c> by default, <c>en</c> by <c>?culture=en</c>) runs first.</param>
    /// <returns>The host.</returns>
    internal static Task<PipelineTestHost> Start(bool localized) =>
        PipelineTestHost.Start(
            static services => services.AddScoped<StatusCodePageScope>().AddLogging(static logging => logging.ClearProviders()),
            app =>
            {
                if (localized)
                {
                    app.UseRequestLocalization(static options =>
                    {
                        options.SetDefaultCulture("pt-BR").AddSupportedCultures("pt-BR", "en").AddSupportedUICultures("pt-BR", "en");
                        options.ApplyCurrentCultureToResponseHeaders = true;
                    });
                }

                app.UseErrorPages("/error", "/not-found");
                app.Map("/error/{code:int}", static (int code, HttpContext context, StatusCodePageScope scope) =>
                {
                    context.Response.Headers[MethodHeader] = context.Request.Method;
                    return $"page {code} {(ReferenceEquals(context.Items[nameof(StatusCodePageScope)], scope) ? "same" : "new")} scope";
                });
                app.MapGet("/not-found", static () => "router page");
                app.MapMethods("/fail", Methods, Fail);
                app.MapMethods("/missing", Methods, NotFoundRemembering);
            });

    private static string Fail(HttpContext context, StatusCodePageScope scope)
    {
        context.Items[nameof(StatusCodePageScope)] = scope;
        throw new InvalidOperationException("Planted failure.");
    }

    private static NotFound NotFoundRemembering(HttpContext context, StatusCodePageScope scope)
    {
        context.Items[nameof(StatusCodePageScope)] = scope;
        return TypedResults.NotFound();
    }
}
