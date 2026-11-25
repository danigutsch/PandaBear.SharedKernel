using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Forwards every cookie event to the application's events, ends the token session on sign-out and, when configured,
/// rejects a cookie whose token session has ended.
/// </summary>
internal sealed class TokenSessionCookieEvents(CookieAuthenticationEvents inner, bool rejectWithoutTokenSession)
    : CookieAuthenticationEvents
{
    /// <inheritdoc />
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        CookiePrincipal.Record(context.HttpContext, context.Scheme.Name, context.Principal);
        await inner.ValidatePrincipal(context).ConfigureAwait(false);
        CookiePrincipal.Record(context.HttpContext, context.Scheme.Name, context.Principal);
        if (!rejectWithoutTokenSession || context.Principal is null)
        {
            return;
        }

        var tokens = context.HttpContext.RequestServices.GetRequiredService<UserTokenManager>();
        if (!await tokens.HasActiveSession(context.Principal, context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override async Task SigningOut(CookieSigningOutContext context)
    {
        var principal = await CookiePrincipal.Find(context.HttpContext, context.Scheme.Name).ConfigureAwait(false);
        if (principal is not null)
        {
            var tokens = context.HttpContext.RequestServices.GetRequiredService<UserTokenManager>();
            await tokens.SignOut(principal, context.HttpContext.RequestAborted).ConfigureAwait(false);
        }

        await inner.SigningOut(context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task CheckSlidingExpiration(CookieSlidingExpirationContext context) => inner.CheckSlidingExpiration(context);

    /// <inheritdoc />
    public override Task SigningIn(CookieSigningInContext context) => inner.SigningIn(context);

    /// <inheritdoc />
    public override Task SignedIn(CookieSignedInContext context) => inner.SignedIn(context);

    /// <inheritdoc />
    public override Task RedirectToLogout(RedirectContext<CookieAuthenticationOptions> context) => inner.RedirectToLogout(context);

    /// <inheritdoc />
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) => inner.RedirectToLogin(context);

    /// <inheritdoc />
    public override Task RedirectToReturnUrl(RedirectContext<CookieAuthenticationOptions> context) => inner.RedirectToReturnUrl(context);

    /// <inheritdoc />
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) => inner.RedirectToAccessDenied(context);
}
