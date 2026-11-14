using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Finds the principal of a request's authentication cookie, which can differ from <see cref="HttpContext.User" /> when
/// the default scheme is another one or the user was replaced.
/// </summary>
/// <remarks>
/// The cookie events record the principal when the cookie handler validates it. Reading it from that record, instead of
/// authenticating the scheme again, keeps a sign-out raised during validation from re-entering the validation.
/// </remarks>
internal static class CookiePrincipal
{
    private const string ItemKeyPrefix = "PandaBear.AspNetCore.Authentication.Bff.CookiePrincipal:";

    /// <summary>Records the principal the cookie handler validates for this request.</summary>
    /// <param name="context">The request.</param>
    /// <param name="scheme">The cookie scheme.</param>
    /// <param name="principal">The cookie's principal.</param>
    internal static void Record(HttpContext context, string scheme, ClaimsPrincipal? principal)
    {
        context.Items[ItemKeyPrefix + scheme] = principal;
    }

    /// <summary>Returns the principal of the request's cookie, authenticating the scheme only when it was not validated yet.</summary>
    /// <param name="context">The request.</param>
    /// <param name="scheme">The cookie scheme.</param>
    /// <returns>The cookie's principal, or <see langword="null" /> when the request has no valid cookie or the scheme is not registered.</returns>
    internal static async Task<ClaimsPrincipal?> Find(HttpContext context, string scheme)
    {
        if (context.Items.TryGetValue(ItemKeyPrefix + scheme, out var recorded))
        {
            return recorded as ClaimsPrincipal;
        }

        var schemes = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        if (await schemes.GetSchemeAsync(scheme).ConfigureAwait(false) is null)
        {
            return null;
        }

        var result = await context.AuthenticateAsync(scheme).ConfigureAwait(false);
        return result.Principal;
    }
}
