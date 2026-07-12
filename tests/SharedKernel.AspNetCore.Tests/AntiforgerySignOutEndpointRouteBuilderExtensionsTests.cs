using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;

namespace SharedKernel.AspNetCore.Tests;

[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class AntiforgerySignOutEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public void Rejects_an_external_sign_out_redirect()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        // Act
        Action action = () => app.MapAntiforgeryProtectedSignOut(
            "/logout",
            CookieAuthenticationDefaults.AuthenticationScheme,
            "remote",
            "https://attacker.example.test");

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Accepts_a_local_uri_sign_out_redirect()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        // Act
        var endpoint = app.MapAntiforgeryProtectedSignOut(
            "/logout",
            CookieAuthenticationDefaults.AuthenticationScheme,
            "remote",
            new Uri("/", UriKind.Relative));

        // Assert
        endpoint.ShouldNotBeNull();
    }
}
