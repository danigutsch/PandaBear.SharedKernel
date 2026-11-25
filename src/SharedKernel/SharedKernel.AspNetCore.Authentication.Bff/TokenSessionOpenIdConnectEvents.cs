using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Forwards every OpenID Connect event to the application's events and, after a validated login, ends the token session of
/// the cookie the login replaces and starts the new one from the token endpoint response.
/// </summary>
internal sealed class TokenSessionOpenIdConnectEvents(OpenIdConnectEvents inner) : OpenIdConnectEvents
{
    private const string UnusableTokenResponse = "The identity provider did not return a usable token response.";

    /// <inheritdoc />
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        await inner.TokenValidated(context).ConfigureAwait(false);
        if (context.Result is not null || context.Principal is null)
        {
            return;
        }

        var response = context.TokenEndpointResponse;
        if (response is null && !string.IsNullOrEmpty(context.ProtocolMessage?.Code))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(response?.AccessToken) || !TryReadLifetime(response.ExpiresIn, out var expiresIn))
        {
            context.Fail(UnusableTokenResponse);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var options = services.GetRequiredService<IOptions<UserTokenManagementOptions>>().Value;
        var manager = services.GetRequiredService<UserTokenManager>();
        if (await CookiePrincipal.Find(context.HttpContext, options.CookieScheme).ConfigureAwait(false) is { } previous)
        {
            await manager.SignOut(previous, context.HttpContext.RequestAborted).ConfigureAwait(false);
        }

        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var tokens = new UserTokenSet(
            response.AccessToken,
            expiresIn is { } lifetime
                ? now.AddSeconds(Math.Min(lifetime, (long)TimeSpan.FromDays(366).TotalSeconds))
                : now.Add(options.DefaultAccessTokenLifetime),
            response.RefreshToken,
            response.IdToken,
            response.TokenType,
            response.Scope);
        await manager.StartSession(context.Principal, tokens, context.HttpContext.RequestAborted).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task AccessDenied(AccessDeniedContext context) => inner.AccessDenied(context);

    /// <inheritdoc />
    public override Task RemoteFailure(RemoteFailureContext context) => inner.RemoteFailure(context);

    /// <inheritdoc />
    public override Task TicketReceived(TicketReceivedContext context) => inner.TicketReceived(context);

    /// <inheritdoc />
    public override Task AuthenticationFailed(AuthenticationFailedContext context) => inner.AuthenticationFailed(context);

    /// <inheritdoc />
    public override Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context) => inner.AuthorizationCodeReceived(context);

    /// <inheritdoc />
    public override Task MessageReceived(MessageReceivedContext context) => inner.MessageReceived(context);

    /// <inheritdoc />
    public override Task RedirectToIdentityProvider(RedirectContext context) => inner.RedirectToIdentityProvider(context);

    /// <inheritdoc />
    public override Task RedirectToIdentityProviderForSignOut(RedirectContext context) => inner.RedirectToIdentityProviderForSignOut(context);

    /// <inheritdoc />
    public override Task SignedOutCallbackRedirect(RemoteSignOutContext context) => inner.SignedOutCallbackRedirect(context);

    /// <inheritdoc />
    public override Task RemoteSignOut(RemoteSignOutContext context) => inner.RemoteSignOut(context);

    /// <inheritdoc />
    public override Task TokenResponseReceived(TokenResponseReceivedContext context) => inner.TokenResponseReceived(context);

    /// <inheritdoc />
    public override Task UserInformationReceived(UserInformationReceivedContext context) => inner.UserInformationReceived(context);

    /// <inheritdoc />
    public override Task PushAuthorization(PushedAuthorizationContext context) => inner.PushAuthorization(context);

    /// <summary>Reads <c>expires_in</c>, which may be absent (RFC 6749 section 5.1) but otherwise must be a positive whole number.</summary>
    private static bool TryReadLifetime(string? value, out long? seconds)
    {
        seconds = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            return false;
        }

        seconds = parsed;
        return true;
    }
}
