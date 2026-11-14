using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Creates authentication event contexts against the configured schemes of a <see cref="BffTestHost" />.</summary>
internal static class AuthenticationEventContexts
{
    /// <summary>A validated-token context for a fresh login of "alice" with the given token endpoint response.</summary>
    /// <param name="host">The host.</param>
    /// <param name="response">The token endpoint response, or <see langword="null" /> for an ID token from the authorization response.</param>
    /// <param name="authorizationResponse">The authorization response.</param>
    /// <returns>The context; its options carry the configured events.</returns>
    internal static TokenValidatedContext TokenValidated(BffTestHost host, OpenIdConnectMessage? response, OpenIdConnectMessage? authorizationResponse = null)
    {
        var options = host.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext { RequestServices = host.CreateRequestServices() };
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "alice")], "oidc"));
        return new TokenValidatedContext(
            httpContext,
            new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler)),
            options,
            principal,
            new AuthenticationProperties())
        {
            TokenEndpointResponse = response,
            ProtocolMessage = authorizationResponse ?? new OpenIdConnectMessage()
        };
    }

    /// <summary>A cookie validation context for the user.</summary>
    /// <param name="host">The host.</param>
    /// <param name="user">The cookie's principal.</param>
    /// <returns>The context; its options carry the configured events.</returns>
    internal static CookieValidatePrincipalContext ValidatePrincipal(BffTestHost host, ClaimsPrincipal user)
    {
        var options = CookieOptions(host);
        var httpContext = new DefaultHttpContext { RequestServices = host.CreateRequestServices() };
        var ticket = new AuthenticationTicket(user, CookieAuthenticationDefaults.AuthenticationScheme);
        return new CookieValidatePrincipalContext(httpContext, CookieScheme(), options, ticket);
    }

    /// <summary>A cookie sign-out context for the signed-in user.</summary>
    /// <param name="host">The host.</param>
    /// <param name="user">The signed-in user.</param>
    /// <returns>The context; its options carry the configured events.</returns>
    internal static CookieSigningOutContext SigningOut(BffTestHost host, ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { RequestServices = host.CreateRequestServices(), User = user };
        return new CookieSigningOutContext(httpContext, CookieScheme(), CookieOptions(host), new AuthenticationProperties(), new CookieOptions());
    }

    private static CookieAuthenticationOptions CookieOptions(BffTestHost host)
    {
        return host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static AuthenticationScheme CookieScheme()
    {
        return new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
    }
}
