using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Forwards every OpenID Connect event to the application's events and, after a validated login, starts the token session
/// from the token endpoint response.
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
        if (string.IsNullOrWhiteSpace(response?.AccessToken)
            || !long.TryParse(response.ExpiresIn, NumberStyles.None, CultureInfo.InvariantCulture, out var expiresIn)
            || expiresIn <= 0)
        {
            context.Fail(UnusableTokenResponse);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var tokens = new UserTokenSet(
            response.AccessToken,
            now.AddSeconds(Math.Min(expiresIn, (long)TimeSpan.FromDays(366).TotalSeconds)),
            response.RefreshToken,
            response.IdToken,
            response.TokenType,
            response.Scope);
        await services.GetRequiredService<UserTokenManager>()
            .StartSession(context.Principal, tokens, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);
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
}
