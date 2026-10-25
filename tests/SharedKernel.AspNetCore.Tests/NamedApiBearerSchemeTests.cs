using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace PandaBear.AspNetCore.Tests;

/// <summary>
/// Verifies named exact-audience bearer schemes for hosts that co-host modules.
/// </summary>
[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class NamedApiBearerSchemeTests
{
    [Fact]
    public async Task Registers_each_scheme_with_its_own_exact_audience_and_the_first_as_default()
    {
        // Arrange
        await using var host = NamedApiBearerSchemeHost.Create();

        // Act
        var catalog = host.Options(NamedApiBearerSchemeHost.CatalogScheme);
        var branding = host.Options(NamedApiBearerSchemeHost.BrandingScheme);
        var defaultScheme = await host.DefaultScheme();
        var transformed = await host.ClaimsTransformation.TransformAsync(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ApiAuthenticationDefaults.RolesClaimType, "Admin")], "test")));

        // Assert
        catalog.TokenValidationParameters.ValidAudience.ShouldBe("catalog-api");
        branding.TokenValidationParameters.ValidAudience.ShouldBe("branding-api");
        branding.TokenValidationParameters.ValidAlgorithms.ShouldBe([SecurityAlgorithms.RsaSha256]);
        defaultScheme.ShouldNotBeNull().Name.ShouldBe(NamedApiBearerSchemeHost.CatalogScheme);
        transformed.FindAll(ApiAuthenticationDefaults.PermissionClaimType).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_token_for_one_module_does_not_validate_for_the_other_scheme()
    {
        // Arrange
        await using var host = NamedApiBearerSchemeHost.Create();
        using var rsa = RSA.Create();
        var signingKey = new RsaSecurityKey(rsa);
        var catalogToken = ApiAuthenticationTokenFactory.Create(
            signingKey,
            "https://identity.example.test/realms/example",
            ["catalog-api"]);
        var brandingParameters = host.Options(NamedApiBearerSchemeHost.BrandingScheme).TokenValidationParameters.Clone();
        brandingParameters.IssuerSigningKey = signingKey;

        // Act
        var result = await new JwtSecurityTokenHandler().ValidateTokenAsync(catalogToken, brandingParameters);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Each_scheme_maps_roles_with_only_its_own_permission_table()
    {
        // Arrange
        await using var host = NamedApiBearerSchemeHost.Create();
        var options = host.Options(NamedApiBearerSchemeHost.BrandingScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ApiAuthenticationDefaults.RolesClaimType, "Admin"),
                new Claim(ApiAuthenticationDefaults.PermissionClaimType, "catalog.write")
            ],
            "test"));
        var context = new TokenValidatedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme(NamedApiBearerSchemeHost.BrandingScheme, null, typeof(JwtBearerHandler)),
            options)
        {
            Principal = principal
        };

        // Act
        await options.Events.TokenValidated(context);

        // Assert
        principal.FindAll(ApiAuthenticationDefaults.PermissionClaimType).Select(static claim => claim.Value)
            .ShouldBe(["branding.write"]);
    }

    [Fact]
    public async Task Generation_mode_registers_authorization_without_bearer_schemes()
    {
        // Arrange
        await using var host = NamedApiBearerSchemeHost.Create(registerBearerAuthentication: false);

        // Act
        var scheme = await host.Scheme(NamedApiBearerSchemeHost.BrandingScheme);

        // Assert
        scheme.ShouldBeNull();
    }

    [Theory]
    [InlineData("same", "catalog-api", "same", "branding-api")]
    [InlineData("CatalogBearer", "shared-api", "BrandingBearer", "shared-api")]
    public void Rejects_schemes_that_share_a_name_or_an_audience(string firstName, string firstAudience, string secondName, string secondAudience)
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var permissions = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        ApiBearerScheme[] schemes = [new(firstName, firstAudience, permissions), new(secondName, secondAudience, permissions)];

        // Act
        var add = () => services.AddApiSecurity(
            ApiAuthenticationTestConfiguration.Create(
                "https://identity.example.test/realms/example",
                "https://identity.example.test/realms/example"),
            new TestHostEnvironment("SharedKernel.AspNetCore.Tests"),
            schemes);

        // Assert
        add.ShouldThrow<ArgumentException>();
    }
}
