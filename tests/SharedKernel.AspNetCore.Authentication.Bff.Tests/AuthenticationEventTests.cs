using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

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
    [InlineData(" ", "300")]
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_login_without_expires_in_uses_the_default_access_token_lifetime(string? expiresIn)
    {
        // Arrange
        using var host = new BffTestHost(options => options.DefaultAccessTokenLifetime = TimeSpan.FromMinutes(2));
        var context = AuthenticationEventContexts.TokenValidated(host, new OpenIdConnectMessage
        {
            AccessToken = "login-access",
            RefreshToken = "login-refresh",
            ExpiresIn = expiresIn
        });

        // Act
        await context.Options.Events.TokenValidated(context);

        // Assert
        context.Result.ShouldBeNull();
        var tokens = await host.Manager.GetTokens(context.Principal!, TestContext.Current.CancellationToken);
        tokens!.AccessTokenExpiresAt.ShouldBe(BffTestHost.Start.AddMinutes(2));
        tokens.RefreshToken.ShouldBe("login-refresh");
    }

    [Fact]
    public async Task The_hybrid_flow_ID_token_from_the_authorization_response_waits_for_the_code_redemption()
    {
        // Arrange
        using var host = new BffTestHost();
        var context = AuthenticationEventContexts.TokenValidated(host, response: null, new OpenIdConnectMessage { Code = "code", IdToken = "front-channel-id" });

        // Act
        await context.Options.Events.TokenValidated(context);

        // Assert
        context.Result.ShouldBeNull();
        host.Cache.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_login_without_a_token_endpoint_response_or_code_fails()
    {
        // Arrange
        using var host = new BffTestHost();
        var context = AuthenticationEventContexts.TokenValidated(host, response: null, new OpenIdConnectMessage { IdToken = "front-channel-id" });

        // Act
        await context.Options.Events.TokenValidated(context);

        // Assert
        context.Result.ShouldNotBeNull();
        context.Result.Failure.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_login_over_an_existing_cookie_ends_and_revokes_the_previous_token_session()
    {
        // Arrange
        using var host = new BffTestHost();
        var previous = await host.SignIn("alice", refreshToken: "previous-refresh");
        var callback = AuthenticationEventContexts.CookieRequest(host, cookieUser: previous, currentUser: new ClaimsPrincipal(new ClaimsIdentity()));
        var context = AuthenticationEventContexts.TokenValidated(
            host,
            new OpenIdConnectMessage { AccessToken = "login-access", RefreshToken = "login-refresh", ExpiresIn = "300" },
            request: callback);

        // Act
        await context.Options.Events.TokenValidated(context);

        // Assert
        var previousActive = await host.Manager.HasActiveSession(previous, TestContext.Current.CancellationToken);
        previousActive.ShouldBeFalse();
        var tokens = await host.Manager.GetTokens(context.Principal!, TestContext.Current.CancellationToken);
        tokens!.RefreshToken.ShouldBe("login-refresh");
        var revocation = host.IdentityProvider.Requests.Where(request => request.Uri == FakeIdentityProvider.RevocationEndpoint).ShouldHaveSingleItem();
        revocation.Form["token"].ShouldBe("previous-refresh");
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
    public async Task Cookie_sign_out_ends_the_token_session_of_the_cookie_being_signed_out()
    {
        // Arrange
        using var host = new BffTestHost();
        var alice = await host.SignIn("alice");
        var bob = await host.SignIn("bob");
        var request = AuthenticationEventContexts.CookieRequest(host, cookieUser: alice, currentUser: bob);

        // Act
        await request.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Assert
        var aliceActive = await host.Manager.HasActiveSession(alice, TestContext.Current.CancellationToken);
        var bobActive = await host.Manager.HasActiveSession(bob, TestContext.Current.CancellationToken);
        aliceActive.ShouldBeFalse();
        bobActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_cookie_rejected_for_an_expired_session_signs_out_that_session_without_recursing()
    {
        // Arrange
        using var host = new BffTestHost(options => options.SessionLifetime = TimeSpan.FromMinutes(1));
        var alice = await host.SignIn("alice", expiresIn: TimeSpan.FromHours(1));
        host.Time.Advance(TimeSpan.FromMinutes(2));
        var request = AuthenticationEventContexts.CookieRequest(host, cookieUser: alice, currentUser: new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        var result = await request.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Assert
        result.Succeeded.ShouldBeFalse();
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
