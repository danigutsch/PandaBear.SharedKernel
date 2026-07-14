using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Maps antiforgery-protected sign-out endpoints for browser authentication sessions.
/// </summary>
public static class AntiforgerySignOutEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps an authorized POST endpoint that validates antiforgery tokens before signing out locally and remotely.
    /// </summary>
    /// <param name="endpoints">The route builder that maps the endpoint.</param>
    /// <param name="pattern">The sign-out endpoint route pattern.</param>
    /// <param name="localAuthenticationScheme">The local session authentication scheme.</param>
    /// <param name="remoteAuthenticationScheme">The remote identity-provider authentication scheme.</param>
    /// <param name="redirectUri">The local URI to use after remote sign-out.</param>
    /// <returns>The configured endpoint.</returns>
    [RequiresDynamicCode("Maps a route handler that relies on runtime-generated code.")]
    [RequiresUnreferencedCode("Maps a route handler that requires unreferenced application code.")]
    public static IEndpointConventionBuilder MapAntiforgeryProtectedSignOut(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string localAuthenticationScheme,
        string remoteAuthenticationScheme,
        string redirectUri)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(localAuthenticationScheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteAuthenticationScheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);

        if (!IsLocalRedirectUri(redirectUri))
        {
            throw new ArgumentException("The sign-out redirect URI must be a local path.", nameof(redirectUri));
        }

        return endpoints.MapPost(pattern, async (HttpContext context, IAntiforgery antiforgery) =>
            {
                if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                await context.SignOutAsync(localAuthenticationScheme).ConfigureAwait(false);
                await context.SignOutAsync(
                    remoteAuthenticationScheme,
                    new AuthenticationProperties { RedirectUri = redirectUri }).ConfigureAwait(false);
            })
            .RequireAuthorization();
    }

    /// <summary>
    /// Maps an authorized POST endpoint using a URI value for the local sign-out redirect.
    /// </summary>
    /// <param name="endpoints">The route builder that maps the endpoint.</param>
    /// <param name="pattern">The sign-out endpoint route pattern.</param>
    /// <param name="localAuthenticationScheme">The local session authentication scheme.</param>
    /// <param name="remoteAuthenticationScheme">The remote identity-provider authentication scheme.</param>
    /// <param name="redirectUri">The local URI to use after remote sign-out.</param>
    /// <returns>The configured endpoint.</returns>
    [RequiresDynamicCode("Maps a route handler that relies on runtime-generated code.")]
    [RequiresUnreferencedCode("Maps a route handler that requires unreferenced application code.")]
    public static IEndpointConventionBuilder MapAntiforgeryProtectedSignOut(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string localAuthenticationScheme,
        string remoteAuthenticationScheme,
        Uri redirectUri)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);

        return MapAntiforgeryProtectedSignOut(
            endpoints,
            pattern,
            localAuthenticationScheme,
            remoteAuthenticationScheme,
            redirectUri.OriginalString);
    }

    private static bool IsLocalRedirectUri(string redirectUri)
    {
        return redirectUri.StartsWith('/')
               && !redirectUri.StartsWith("//", StringComparison.Ordinal)
               && !redirectUri.StartsWith("/\\", StringComparison.Ordinal);
    }
}
