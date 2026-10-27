using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Disposable service provider hosting two named API bearer schemes, one for a catalog module and one for a branding module.</summary>
internal sealed class NamedApiBearerSchemeHost : IAsyncDisposable
{
    /// <summary>Name of the catalog module's bearer scheme.</summary>
    public const string CatalogScheme = "CatalogBearer";
    /// <summary>Name of the branding module's bearer scheme.</summary>
    public const string BrandingScheme = "BrandingBearer";

    private readonly ServiceProvider _provider;

    private NamedApiBearerSchemeHost(ServiceProvider provider)
    {
        _provider = provider;
    }

    /// <summary>Gets the two scheme definitions: "catalog-api" granting "catalog.write" and "branding-api" granting "branding.write" to the Admin role.</summary>
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

    /// <summary>Builds a host by registering <see cref="Schemes" /> through <c>AddApiSecurity</c> against a fixed HTTPS authority and issuer.</summary>
    /// <param name="registerBearerAuthentication">Whether to force bearer registration on or off, or <see langword="null" /> for the default.</param>
    /// <returns>The built host.</returns>
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

    /// <summary>Gets the JWT bearer options registered for a scheme.</summary>
    /// <param name="scheme">The scheme name.</param>
    /// <returns>The options for that scheme.</returns>
    public JwtBearerOptions Options(string scheme) =>
        _provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(scheme);

    /// <summary>Looks up an authentication scheme by name, returning null when no scheme provider or no such scheme is registered.</summary>
    /// <param name="name">The scheme name.</param>
    /// <returns>The scheme, or <see langword="null" />.</returns>
    public async Task<AuthenticationScheme?> Scheme(string name) =>
        _provider.GetService<IAuthenticationSchemeProvider>() is { } schemes
            ? await schemes.GetSchemeAsync(name).ConfigureAwait(false)
            : null;

    /// <summary>Gets the default authenticate scheme from the scheme provider.</summary>
    /// <returns>The default authenticate scheme, or <see langword="null" /> when none is configured.</returns>
    public Task<AuthenticationScheme?> DefaultScheme() =>
        _provider.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync();

    /// <summary>Gets the default challenge scheme from the scheme provider.</summary>
    /// <returns>The default challenge scheme, or <see langword="null" /> when none is configured.</returns>
    public Task<AuthenticationScheme?> DefaultChallengeScheme() =>
        _provider.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultChallengeSchemeAsync();

    /// <summary>Gets the registered claims transformation.</summary>
    public IClaimsTransformation ClaimsTransformation => _provider.GetRequiredService<IClaimsTransformation>();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
