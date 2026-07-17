using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
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
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
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
        jwt.ConfigurationManager.ShouldNotBeNull();
        jwt.Audience.ShouldBe("admin-api");
        jwt.MapInboundClaims.ShouldBeFalse();
        jwt.TokenValidationParameters.ValidIssuer.ShouldBe("https://identity.example.test/realms/viajantes");
        jwt.TokenValidationParameters.ValidAudience.ShouldBe("admin-api");
        jwt.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        jwt.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        jwt.TokenValidationParameters.ValidateIssuerSigningKey.ShouldBeTrue();
        jwt.TokenValidationParameters.ValidateLifetime.ShouldBeTrue();
        jwt.TokenValidationParameters.ClockSkew.ShouldBe(TimeSpan.FromMinutes(2));
        jwt.TokenValidationParameters.ValidAlgorithms.ShouldBe([SecurityAlgorithms.RsaSha256]);
        var permissionValues = twice.FindAll(ApiAuthenticationDefaults.PermissionClaimType).Select(static claim => claim.Value).ToArray();
        permissionValues.ShouldContain("tours.read");
        permissionValues.ShouldContain("tours.write");
        permissionValues.Length.ShouldBe(2);
        var fallbackPolicy = authorization.FallbackPolicy.ShouldNotBeNull();
        fallbackPolicy.Requirements.ShouldContain(requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task Configures_authorization_without_a_bearer_scheme()
    {
        // Arrange
        await using var host = ApiAuthenticationTestHost.CreateAuthorizationOnly(
            new Dictionary<string, IReadOnlyCollection<string>>());

        // Act
        var authorization = host.AuthorizationOptions;
        var hasBearerAuthenticationScheme = await host.HasBearerAuthenticationScheme();

        // Assert
        authorization.FallbackPolicy.ShouldNotBeNull();
        hasBearerAuthenticationScheme.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenApi_generation_configuration_marker_does_not_disable_bearer_authentication()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ApiAuthenticationDefaults.AuthorityConfigurationKey] = "https://identity.example.test/realms/viajantes",
                [ApiAuthenticationDefaults.IssuerConfigurationKey] = "https://identity.example.test/realms/viajantes",
                ["OpenApi:BuildGeneration"] = bool.TrueString
            })
            .Build();
        await using var host = ApiAuthenticationTestHost.CreateImplicitSecurity(
            configuration,
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());

        // Act
        var hasBearerAuthenticationScheme = await host.HasBearerAuthenticationScheme();
        var bearerOptions = host.BearerOptions;

        // Assert
        hasBearerAuthenticationScheme.ShouldBeTrue();
        bearerOptions.Audience.ShouldBe("admin-api");
    }

    [Fact]
    public void Rejects_the_generation_environment_without_the_document_generator_identity()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(string.Empty, string.Empty);
        var environment = new TestHostEnvironment("SharedKernel.AspNetCore.Tests") { EnvironmentName = "OpenApiGeneration" };

        // Act
        Action action = () => ApiAuthenticationTestHost.CreateImplicitSecurity(
            configuration,
            environment,
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task Accepts_a_token_with_only_the_expected_audience()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());
        using var rsa = RSA.Create();
        var signingKey = new RsaSecurityKey(rsa);
        var token = ApiAuthenticationTokenFactory.Create(
            signingKey,
            "https://identity.example.test/realms/viajantes",
            ["admin-api"]);
        var validationParameters = host.BearerOptions.TokenValidationParameters.Clone();
        validationParameters.IssuerSigningKey = signingKey;
        var tokenHandler = new JwtSecurityTokenHandler();

        // Act
        var validationResult = await tokenHandler.ValidateTokenAsync(token, validationParameters);

        // Assert
        validationResult.IsValid.ShouldBeTrue();
        validationResult.ClaimsIdentity.ShouldNotBeNull();
        validationResult.ClaimsIdentity.IsAuthenticated.ShouldBeTrue();
    }

    [Fact]
    public async Task Rejects_a_token_without_an_audience()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());
        using var rsa = RSA.Create();
        var signingKey = new RsaSecurityKey(rsa);
        var token = ApiAuthenticationTokenFactory.Create(
            signingKey,
            "https://identity.example.test/realms/viajantes",
            []);
        var validationParameters = host.BearerOptions.TokenValidationParameters.Clone();
        validationParameters.IssuerSigningKey = signingKey;
        var tokenHandler = new JwtSecurityTokenHandler();

        // Act
        var validationResult = await tokenHandler.ValidateTokenAsync(token, validationParameters);

        // Assert
        validationResult.IsValid.ShouldBeFalse();
        validationResult.Exception.ShouldBeOfType<SecurityTokenInvalidAudienceException>();
    }

    [Fact]
    public async Task Rejects_a_token_with_a_wrong_audience()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());
        using var rsa = RSA.Create();
        var signingKey = new RsaSecurityKey(rsa);
        var token = ApiAuthenticationTokenFactory.Create(
            signingKey,
            "https://identity.example.test/realms/viajantes",
            ["catalog-api"]);
        var validationParameters = host.BearerOptions.TokenValidationParameters.Clone();
        validationParameters.IssuerSigningKey = signingKey;
        var tokenHandler = new JwtSecurityTokenHandler();

        // Act
        var validationResult = await tokenHandler.ValidateTokenAsync(token, validationParameters);

        // Assert
        validationResult.IsValid.ShouldBeFalse();
        validationResult.Exception.ShouldBeOfType<SecurityTokenInvalidAudienceException>();
    }

    [Fact]
    public async Task Rejects_a_token_with_the_expected_and_an_additional_audience()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());
        using var rsa = RSA.Create();
        var signingKey = new RsaSecurityKey(rsa);
        var token = ApiAuthenticationTokenFactory.Create(
            signingKey,
            "https://identity.example.test/realms/viajantes",
            ["admin-api", "catalog-api"]);
        var validationParameters = host.BearerOptions.TokenValidationParameters.Clone();
        validationParameters.IssuerSigningKey = signingKey;
        var tokenHandler = new JwtSecurityTokenHandler();

        // Act
        var validationResult = await tokenHandler.ValidateTokenAsync(token, validationParameters);

        // Assert
        validationResult.IsValid.ShouldBeFalse();
        validationResult.Exception.ShouldBeOfType<SecurityTokenInvalidAudienceException>();
    }

    [Fact]
    public async Task Rejects_a_second_audience_without_enumerating_remaining_values()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "https://identity.example.test/realms/viajantes");
        await using var host = ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());
        var audienceValidator = host.BearerOptions.TokenValidationParameters.AudienceValidator
            ?? throw new InvalidOperationException("The bearer audience validator was not configured.");

        // Act
        var isValid = audienceValidator(
            new ExactAudienceEnumerable(),
            new JwtSecurityToken(),
            new TokenValidationParameters());

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_missing_authority_and_issuer_outside_development()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(string.Empty, string.Empty);

        // Act
        Action action = () => ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Rejects_a_missing_issuer_when_the_authority_is_present()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            string.Empty);

        // Act
        Action action = () => ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Rejects_an_invalid_issuer_when_the_authority_is_valid()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "https://identity.example.test/realms/viajantes",
            "ftp://identity.example.test/realms/viajantes");

        // Act
        Action action = () => ApiAuthenticationTestHost.Create(
            configuration,
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            "admin-api",
            new Dictionary<string, IReadOnlyCollection<string>>());

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Rejects_missing_authority_and_issuer_in_development()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(string.Empty, string.Empty);
        var environment = new TestHostEnvironment("SharedKernel.AspNetCore.Tests") { EnvironmentName = Environments.Development };

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
    public async Task Permits_http_authority_only_with_the_development_opt_in()
    {
        // Arrange
        var configuration = ApiAuthenticationTestConfiguration.Create(
            "http://localhost:8080/realms/viajantes",
            "http://localhost:8080/realms/viajantes",
            allowHttpDevelopmentAuthority: true);
        var environment = new TestHostEnvironment("SharedKernel.AspNetCore.Tests") { EnvironmentName = Environments.Development };

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
        var environment = new TestHostEnvironment("SharedKernel.AspNetCore.Tests") { EnvironmentName = Environments.Development };

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
        var environment = new TestHostEnvironment("SharedKernel.AspNetCore.Tests") { EnvironmentName = Environments.Development };

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
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
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
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
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
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
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
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
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
