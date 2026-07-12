using System.Security.Claims;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>
/// Verifies the shared bearer-authentication boundary configuration.
/// </summary>
[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class ApiAuthenticationServiceCollectionExtensionsTests
{
    [Fact]
    public async Task Configures_strict_bearer_validation_and_maps_permissions_once()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        var permissions = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["Admin"] = ["tours.read", "tours.write"]
        };
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment(),
            "admin-api",
            permissions);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ApiAuthenticationDefaults.RolesClaimType, "Admin")], "test"));

        // Act
        var jwt = host.BearerOptions;
        var once = await host.ClaimsTransformation.TransformAsync(principal);
        var twice = await host.ClaimsTransformation.TransformAsync(once);
        var authorization = host.AuthorizationOptions;

        // Assert
        jwt.Authority.ShouldBe("https://identity.example.test/realms/viajantes");
        jwt.RequireHttpsMetadata.ShouldBeTrue();
        jwt.Audience.ShouldBe("admin-api");
        jwt.MapInboundClaims.ShouldBeFalse();
        jwt.TokenValidationParameters.ValidIssuer.ShouldBe("https://identity.example.test/realms/viajantes");
        jwt.TokenValidationParameters.ValidAudience.ShouldBe("admin-api");
        jwt.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        jwt.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        jwt.TokenValidationParameters.ValidateIssuerSigningKey.ShouldBeTrue();
        jwt.TokenValidationParameters.ValidateLifetime.ShouldBeTrue();
        jwt.TokenValidationParameters.ClockSkew.ShouldBe(TimeSpan.FromMinutes(2));
        jwt.TokenValidationParameters.ValidAlgorithms.Contains(SecurityAlgorithms.RsaSha256).ShouldBeTrue();
        var permissionValues = twice.FindAll(ApiAuthenticationDefaults.PermissionClaimType).Select(static claim => claim.Value).ToArray();
        permissionValues.ShouldContain("tours.read");
        permissionValues.ShouldContain("tours.write");
        permissionValues.Length.ShouldBe(2);
        authorization.FallbackPolicy.ShouldNotBeNull();
    }

    [Fact]
    public void Rejects_missing_authority_and_issuer_outside_development()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(string.Empty, string.Empty);

        // Act
        Action action = () => ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment(),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task Permits_http_authority_only_with_the_development_opt_in()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "http://localhost:8080/realms/viajantes",
            "http://localhost:8080/realms/viajantes",
            allowHttpDevelopmentAuthority: true);
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            environment,
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());
        var jwt = host.BearerOptions;

        // Assert
        jwt.RequireHttpsMetadata.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_http_authority_without_the_development_opt_in()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "http://localhost:8080/realms/viajantes",
            "http://localhost:8080/realms/viajantes");
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        Action action = () => ApiAuthenticationTestHost.Create(
            configuration,
            environment,
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Rejects_unsupported_authority_schemes_with_the_development_opt_in()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "ftp://identity.example.test/realms/viajantes",
            "ftp://identity.example.test/realms/viajantes",
            allowHttpDevelopmentAuthority: true);
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        Action action = () => ApiAuthenticationTestHost.Create(
            configuration,
            environment,
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task Leaves_unknown_roles_without_permissions()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment(),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>> { ["Admin"] = ["tours.read"] });
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ApiAuthenticationDefaults.RolesClaimType, "Unknown")], "test"));

        // Act
        var transformed = await host.ClaimsTransformation.TransformAsync(principal);

        // Assert
        transformed.FindAll(ApiAuthenticationDefaults.PermissionClaimType).ShouldBeEmpty();
    }

    [Fact]
    public async Task Removes_permissions_supplied_by_an_unknown_role()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment(),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>> { ["Admin"] = ["tours.read"] });
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ApiAuthenticationDefaults.RolesClaimType, "Unknown"),
            new Claim(ApiAuthenticationDefaults.PermissionClaimType, "tours.write")
        ], "test"));

        // Act
        var transformed = await host.ClaimsTransformation.TransformAsync(principal);

        // Assert
        transformed.FindAll(ApiAuthenticationDefaults.PermissionClaimType).ShouldBeEmpty();
    }

    [Fact]
    public async Task Replaces_permissions_supplied_by_a_mapped_role()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment(),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>> { ["Admin"] = ["tours.read"] });
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ApiAuthenticationDefaults.RolesClaimType, "Admin"),
            new Claim(ApiAuthenticationDefaults.PermissionClaimType, "tours.read"),
            new Claim(ApiAuthenticationDefaults.PermissionClaimType, "tours.write")
        ], "test"));

        // Act
        var transformed = await host.ClaimsTransformation.TransformAsync(principal);
        var permissions = transformed.FindAll(ApiAuthenticationDefaults.PermissionClaimType).Select(static claim => claim.Value).ToArray();

        // Assert
        permissions.ShouldBe(["tours.read"]);
    }

    [Fact]
    public async Task Rebuilds_permissions_across_identities_without_provider_permissions()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment(),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>> { ["Admin"] = ["tours.read", "tours.write"] });
        var principal = new ClaimsPrincipal(
        [
            new ClaimsIdentity(
            [
                new Claim(ApiAuthenticationDefaults.RolesClaimType, "Admin"),
                new Claim(ApiAuthenticationDefaults.PermissionClaimType, "customers.read")
            ], "first"),
            new ClaimsIdentity(
            [
                new Claim(ApiAuthenticationDefaults.RolesClaimType, "Unknown"),
                new Claim(ApiAuthenticationDefaults.PermissionClaimType, "bookings.delete")
            ], "second")
        ]);

        // Act
        var transformed = await host.ClaimsTransformation.TransformAsync(principal);
        var permissions = transformed.FindAll(ApiAuthenticationDefaults.PermissionClaimType)
            .Select(static claim => claim.Value)
            .Order()
            .ToArray();

        // Assert
        permissions.ShouldBe(["tours.read", "tours.write"]);
    }
}
