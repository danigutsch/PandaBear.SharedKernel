using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>Builds services for the current-user accessor tests.</summary>
internal static class CurrentUserTestServices
{
    /// <summary>Builds token management services, optionally with the Blazor Server accessor and a settable circuit user.</summary>
    /// <param name="blazor">Whether to register the Blazor Server accessor.</param>
    /// <returns>The services.</returns>
    internal static ServiceProvider Build(bool blazor)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddDataProtection();
        services.AddAuthentication().AddCookie().AddOpenIdConnect(options => options.Configuration = new());
        services.AddUserTokenManagement();
        if (blazor)
        {
            services.AddScoped<AuthenticationStateProvider, FixedAuthenticationStateProvider>();
            services.AddBlazorServerCurrentUser();
        }

        return services.BuildServiceProvider();
    }

    /// <summary>An authenticated principal with a subject.</summary>
    /// <param name="subject">The subject.</param>
    /// <returns>The principal.</returns>
    internal static ClaimsPrincipal Principal(string subject)
    {
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "test"));
    }
}
