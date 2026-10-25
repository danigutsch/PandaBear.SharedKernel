using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Tests;

internal sealed class NamedApiBearerSchemeHost : IAsyncDisposable
{
    public const string CatalogScheme = "CatalogBearer";
    public const string BrandingScheme = "BrandingBearer";

    private readonly ServiceProvider _provider;

    private NamedApiBearerSchemeHost(ServiceProvider provider)
    {
        _provider = provider;
    }

    public static IReadOnlyList<ApiBearerScheme> Schemes
    {
        get;
    } =
    [
        new(CatalogScheme, "catalog-api", new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["Admin"] = ["catalog.write"]
        }),
        new(BrandingScheme, "branding-api", new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["Admin"] = ["branding.write"]
        })
    ];

    public static NamedApiBearerSchemeHost Create(bool? registerBearerAuthentication = null)
    {
        var services = new ServiceCollection();
        services.AddApiSecurity(
            ApiAuthenticationTestConfiguration.Create(
                "https://identity.example.test/realms/example",
                "https://identity.example.test/realms/example"),
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            Schemes,
            registerBearerAuthentication);
        return new NamedApiBearerSchemeHost(services.BuildServiceProvider());
    }

    public JwtBearerOptions Options(string scheme) =>
        _provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(scheme);

    public async Task<AuthenticationScheme?> Scheme(string name) =>
        _provider.GetService<IAuthenticationSchemeProvider>() is { } schemes
            ? await schemes.GetSchemeAsync(name).ConfigureAwait(false)
            : null;

    public Task<AuthenticationScheme?> DefaultScheme() =>
        _provider.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync();

    public IClaimsTransformation ClaimsTransformation => _provider.GetRequiredService<IClaimsTransformation>();

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
