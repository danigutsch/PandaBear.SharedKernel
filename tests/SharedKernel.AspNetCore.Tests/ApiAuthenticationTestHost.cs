using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SharedKernel.AspNetCore.Tests;

internal sealed class ApiAuthenticationTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private ApiAuthenticationTestHost(ServiceProvider provider)
    {
        _provider = provider;
    }

    public JwtBearerOptions BearerOptions => _provider
        .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
        .Get(JwtBearerDefaults.AuthenticationScheme);

    public IClaimsTransformation ClaimsTransformation => _provider.GetRequiredService<IClaimsTransformation>();

    public AuthorizationOptions AuthorizationOptions => _provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

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

    public ValueTask DisposeAsync()
    {
        return _provider.DisposeAsync();
    }
}
