using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Hooks the token session into the configured OpenID Connect and cookie schemes.
/// </summary>
internal sealed class TokenSessionPostConfigureOptions(IOptions<UserTokenManagementOptions> managementOptions)
    : IPostConfigureOptions<OpenIdConnectOptions>, IPostConfigureOptions<CookieAuthenticationOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, managementOptions.Value.OpenIdConnectScheme, StringComparison.Ordinal))
        {
            return;
        }

        EnsureNoEventsType(options.EventsType, name);
        options.Events = new TokenSessionOpenIdConnectEvents(options.Events ?? new OpenIdConnectEvents());
    }

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, managementOptions.Value.CookieScheme, StringComparison.Ordinal))
        {
            return;
        }

        EnsureNoEventsType(options.EventsType, name);
        options.Events = new TokenSessionCookieEvents(
            options.Events ?? new CookieAuthenticationEvents(),
            managementOptions.Value.RejectCookieWithoutTokenSession);
    }

    private static void EnsureNoEventsType(Type? eventsType, string? scheme)
    {
        if (eventsType is not null)
        {
            throw new InvalidOperationException(
                $"The '{scheme}' scheme sets EventsType, which would bypass the BFF token session. Assign an events instance to Events instead.");
        }
    }
}
