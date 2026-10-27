using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Disposable service provider that exposes the options and services registered by the API security extensions.</summary>
internal sealed class ApiAuthenticationTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private ApiAuthenticationTestHost(ServiceProvider provider)
    {
        _provider = provider;
    }

    /// <summary>Gets the JWT bearer options registered for the default bearer scheme.</summary>
    public JwtBearerOptions BearerOptions => _provider
        .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
        .Get(JwtBearerDefaults.AuthenticationScheme);

    /// <summary>Gets the registered claims transformation that maps roles to permission claims.</summary>
    public IClaimsTransformation ClaimsTransformation => _provider.GetRequiredService<IClaimsTransformation>();

    /// <summary>Gets the configured authorization options, including the fallback policy.</summary>
    public AuthorizationOptions AuthorizationOptions => _provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

    /// <summary>Checks whether an authentication scheme provider exists and has the default JWT bearer scheme registered.</summary>
    /// <returns><see langword="true" /> when the bearer scheme is registered.</returns>
    public async ValueTask<bool> HasBearerAuthenticationScheme()
    {
        var schemeProvider = _provider.GetService<IAuthenticationSchemeProvider>();

        return schemeProvider is not null
            && await schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme).ConfigureAwait(false) is not null;
    }

    /// <summary>Builds a host by registering bearer authentication through <c>AddApiBearerAuthentication</c>.</summary>
    /// <param name="configuration">The configuration holding authority and issuer settings.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="audience">The required token audience.</param>
    /// <param name="permissionsByRole">The permissions granted to each role.</param>
    /// <returns>The built host.</returns>
    public static ApiAuthenticationTestHost Create(
        IConfiguration configuration,
        TestHostEnvironment environment,
        string audience,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
    {
        var services = new ServiceCollection();
        services.AddApiBearerAuthentication(configuration, environment, audience, permissionsByRole);
        return new ApiAuthenticationTestHost(services.BuildServiceProvider());
    }

    /// <summary>Builds a host through <c>AddApiSecurity</c> with bearer authentication registration turned off, so only authorization is configured.</summary>
    /// <param name="permissionsByRole">The permissions granted to each role.</param>
    /// <returns>The built host.</returns>
    public static ApiAuthenticationTestHost CreateAuthorizationOnly(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
    {
        var services = new ServiceCollection();
        services.AddApiSecurity(
            ApiAuthenticationTestConfiguration.Create(string.Empty, string.Empty),
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "test-api",
            permissionsByRole,
            registerBearerAuthentication: false);
        return new ApiAuthenticationTestHost(services.BuildServiceProvider());
    }

    /// <summary>Builds a host through <c>AddApiSecurity</c> with its default settings, letting the extension decide whether to register bearer authentication.</summary>
    /// <param name="configuration">The configuration holding authority and issuer settings.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="audience">The required token audience.</param>
    /// <param name="permissionsByRole">The permissions granted to each role.</param>
    /// <returns>The built host.</returns>
    public static ApiAuthenticationTestHost CreateImplicitSecurity(
        IConfiguration configuration,
        TestHostEnvironment environment,
        string audience,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
    {
        var services = new ServiceCollection();
        services.AddApiSecurity(configuration, environment, audience, permissionsByRole);
        return new ApiAuthenticationTestHost(services.BuildServiceProvider());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _provider.DisposeAsync();
    }
}
