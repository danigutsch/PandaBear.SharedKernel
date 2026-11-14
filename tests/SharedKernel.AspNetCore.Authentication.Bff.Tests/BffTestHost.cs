using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PandaBear.Testing.Http;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>A service provider wired with BFF token management, a fake identity provider, a fake clock and a recording cache.</summary>
internal sealed class BffTestHost : IDisposable
{
    /// <summary>The fake clock's start.</summary>
    internal static readonly DateTimeOffset Start = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;
    private readonly List<IServiceScope> _scopes = [];

    /// <summary>Creates the host.</summary>
    /// <param name="configure">Configures token management.</param>
    /// <param name="revocationEndpoint">Whether discovery names a revocation endpoint.</param>
    /// <param name="services">Adds further services.</param>
    internal BffTestHost(
        Action<UserTokenManagementOptions>? configure = null,
        bool revocationEndpoint = true,
        Action<IServiceCollection>? services = null)
    {
        Cache = new RecordingDistributedCache(Time);
        var collection = new ServiceCollection();
        collection.AddLogging(logging => logging.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
        collection.AddSingleton<TimeProvider>(Time);
        collection.AddSingleton<IDistributedCache>(Cache);
        collection.AddDataProtection().UseEphemeralDataProtectionProvider();
        collection.AddAuthentication()
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.ClientId = "bff-client";
                options.ClientSecret = "bff-secret";
                options.Configuration = new OpenIdConnectConfiguration
                {
                    TokenEndpoint = FakeIdentityProvider.TokenEndpoint.AbsoluteUri,
                    RevocationEndpoint = revocationEndpoint ? FakeIdentityProvider.RevocationEndpoint.AbsoluteUri : null
                };
            });
        collection.AddUserTokenManagement(configure);
        collection.AddHttpClient(UserTokenManagementDefaults.IdentityProviderHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => IdentityProvider);
        services?.Invoke(collection);
        _services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>The fake clock.</summary>
    internal FakeTimeProvider Time { get; } = new(Start);

    /// <summary>The recording cache; it refuses an absolute expiration in the past.</summary>
    internal RecordingDistributedCache Cache
    {
        get;
    }

    /// <summary>The fake identity provider.</summary>
    internal FakeIdentityProvider IdentityProvider { get; } = new();

    /// <summary>The recorded logs.</summary>
    internal RecordingLoggerProvider Logs { get; } = new();

    /// <summary>The services.</summary>
    internal IServiceProvider Services => _services;

    /// <summary>The token manager.</summary>
    internal UserTokenManager Manager => _services.GetRequiredService<UserTokenManager>();

    /// <summary>Signs a user in with a token set that expires after the given time.</summary>
    internal async Task<ClaimsPrincipal> SignIn(
        string subject,
        string accessToken = "access-0",
        string? refreshToken = "refresh-0",
        TimeSpan? expiresIn = null)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "oidc"));
        var tokens = new UserTokenSet(
            accessToken,
            Time.GetUtcNow().Add(expiresIn ?? TimeSpan.FromMinutes(5)),
            refreshToken,
            identityToken: "id-0",
            accessTokenType: "Bearer",
            scope: "openid profile");
        await Manager.StartSession(principal, tokens, CancellationToken.None);
        return principal;
    }

    /// <summary>Creates the services of one request scope; the host disposes it.</summary>
    /// <returns>The scoped services.</returns>
    internal IServiceProvider CreateRequestServices()
    {
        var scope = _services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider;
    }

    /// <summary>Creates a host with an "api" client whose handlers the caller adds and whose server only observes requests.</summary>
    /// <param name="handlers">Adds the handlers under test.</param>
    /// <param name="observe">Observes each request that reaches the server.</param>
    /// <returns>The host.</returns>
    internal static BffTestHost WithApiClient(Action<IHttpClientBuilder> handlers, Action<HttpRequestMessage> observe)
    {
        return new BffTestHost(services: services =>
        {
            services.AddHttpContextAccessor();
            var builder = services.AddHttpClient("api")
                .ConfigurePrimaryHttpMessageHandler(() => new DelegateHttpMessageHandler(request =>
                {
                    observe(request);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }));
            handlers(builder);
        });
    }

    /// <summary>All rendered log text, for checks that no token leaks.</summary>
    internal string AllLogText()
    {
        return string.Join('\n', Logs.Records.Select(record => string.Concat(
            record.Message,
            " ",
            string.Join(' ', record.State.Select(pair => pair.Value)),
            " ",
            record.Exception?.ToString())));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        _services.Dispose();
        Logs.Dispose();
        IdentityProvider.Dispose();
    }
}
