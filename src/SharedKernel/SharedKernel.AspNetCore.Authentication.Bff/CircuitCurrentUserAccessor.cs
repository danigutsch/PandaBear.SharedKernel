using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Reads the user of the running Blazor Server circuit activity, and otherwise the user of the current HTTP request.
/// </summary>
internal sealed class CircuitCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    /// <inheritdoc />
    public async ValueTask<ClaimsPrincipal> GetCurrentUser(CancellationToken ct)
    {
        if (CircuitServicesAccessor.Services?.GetService<AuthenticationStateProvider>() is { } authenticationStateProvider)
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
            return state.User;
        }

        return httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    }
}
