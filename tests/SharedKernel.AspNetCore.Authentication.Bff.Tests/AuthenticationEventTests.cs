using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Tests the OpenID Connect and cookie events that start, check and end a token session.</summary>
public sealed class AuthenticationEventTests
{
    [Fact]
    public async Task A_validated_login_runs_earlier_handlers_then_starts_a_session_with_the_token_response()
    {
        // Arrange
        var previousHandlerRan = false;
        using var host = new BffTestHost(services: services => services.Configure<OpenIdConnectOptions>(
            OpenIdConnectDefaults.AuthenticationScheme,
            options => options.Events.OnTokenValidated = _ =>
            {
                previousHandlerRan = true;
                return Task.CompletedTask;
            }));
        var context = AuthenticationEventContexts.TokenValidated(host, new OpenIdConnectMessage
        {
            AccessToken = "login-access",
            RefreshToken = "login-refresh",
            IdToken = "login-id",
            TokenType = "Bearer",
            ExpiresIn = "300",
            Scope = "openid offline_access"
        });

        // Act
        await context.Options.Events.TokenValidated(context);

        // Assert
        previousHandlerRan.ShouldBeTrue();
        context.Result.ShouldBeNull();
        var principal = context.Principal!;
        principal.FindFirst(BffClaimTypes.SessionId).ShouldNotBeNull();
        var tokens = await host.Manager.GetTokens(principal, TestContext.Current.CancellationToken);
        tokens!.AccessToken.ShouldBe("login-access");
        tokens.RefreshToken.ShouldBe("login-refresh");
        tokens.IdentityToken.ShouldBe("login-id");
        tokens.AccessTokenExpiresAt.ShouldBe(BffTestHost.Start.AddSeconds(300));
    }

    [Theory]
    [InlineData(null, "300")]
    [InlineData("access", null)]
    [InlineData("access", "0")]
    [InlineData("access", "soon")]
    public async Task A_login_without_a_usable_token_response_fails(string? accessToken, string? expiresIn)
    {
        // Arrange
        using var host = new BffTestHost();
        var context = AuthenticationEventContexts.TokenValidated(host, new OpenIdConnectMessage { AccessToken = accessToken, ExpiresIn = expiresIn });

        // Act
        await context.Options.Events.TokenValidated(context);

        // Assert
        context.Result.ShouldNotBeNull();
        context.Result.Failure.ShouldNotBeNull();
        host.Cache.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_login_replaces_session_claims_that_the_principal_already_carried()
    {
        // Arrange
        using var host = new BffTestHost();
        var context = AuthenticationEventContexts.TokenValidated(host, new OpenIdConnectMessage { AccessToken = "a", ExpiresIn = "60" });
        ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(new Claim(BffClaimTypes.SessionId, "forged"));

        // Act
        await context.Options.Events.TokenValidated(context);

        // Assert
        context.Principal.FindAll(BffClaimTypes.SessionId).ShouldHaveSingleItem().Value.ShouldNotBe("forged");
    }

    [Fact]
    public async Task Cookie_sign_out_ends_the_token_session()
    {
        // Arrange
        using var host = new BffTestHost();
        var user = await host.SignIn("alice");
        var context = AuthenticationEventContexts.SigningOut(host, user);

        // Act
        await context.Options.Events.SigningOut(context);

        // Assert
        host.Cache.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_cookie_whose_token_session_is_gone_is_rejected()
    {
        // Arrange
        using var host = new BffTestHost();
        var user = await host.SignIn("alice");
        await host.Manager.SignOut(user, TestContext.Current.CancellationToken);
        var context = AuthenticationEventContexts.ValidatePrincipal(host, user);

        // Act
        await context.Options.Events.ValidatePrincipal(context);

        // Assert
        context.Principal.ShouldBeNull();
    }

    [Fact]
    public async Task A_cookie_with_a_live_token_session_is_kept()
    {
        // Arrange
        using var host = new BffTestHost();
        var user = await host.SignIn("alice");
        var context = AuthenticationEventContexts.ValidatePrincipal(host, user);

        // Act
        await context.Options.Events.ValidatePrincipal(context);

        // Assert
        context.Principal.ShouldBeSameAs(user);
    }

    [Fact]
    public void Custom_events_types_are_refused_because_they_would_bypass_the_token_session()
    {
        // Arrange
        using var host = new BffTestHost(services: services =>
            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options => options.EventsType = typeof(OpenIdConnectEvents)));
        var monitor = host.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();

        // Act
        Action read = () => monitor.Get(OpenIdConnectDefaults.AuthenticationScheme);

        // Assert
        read.ShouldThrow<InvalidOperationException>();
    }
}
