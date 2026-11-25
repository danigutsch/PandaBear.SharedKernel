using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>An authentication state provider that returns a settable user.</summary>
internal sealed class FixedAuthenticationStateProvider : AuthenticationStateProvider
{
    /// <summary>The user of the circuit.</summary>
    internal ClaimsPrincipal User { get; set; } = new(new ClaimsIdentity());

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(User));
    }
}
