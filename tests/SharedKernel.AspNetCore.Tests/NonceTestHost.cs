using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>Starts a host with the nonce service and security headers, and reads the policy header of a response.</summary>
internal static class NonceTestHost
{
    /// <summary>Starts a host whose <c>/nonce</c> endpoint answers the nonce read twice as <c>first|second</c> and whose <c>/plain</c> never reads it.</summary>
    /// <param name="policy">The Content Security Policy.</param>
    /// <returns>The started host.</returns>
    internal static Task<PipelineTestHost> Start(string policy) => PipelineTestHost.Start(
        services => services.AddCspNonce(),
        app =>
        {
            app.UseSecurityHeaders(policy);
            app.MapGet("/nonce", (HttpContext context) =>
            {
                var first = context.RequestServices.GetRequiredService<ICspNonce>().Value;
                var second = context.RequestServices.GetRequiredService<ICspNonce>().Value;
                return $"{first}|{second}";
            });
            app.MapGet("/plain", () => "ok");
        });

    /// <summary>Gets the Content-Security-Policy header of a response.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The header value.</returns>
    internal static string Header(HttpResponseMessage response) => string.Join(",", response.Headers.GetValues("Content-Security-Policy"));
}
