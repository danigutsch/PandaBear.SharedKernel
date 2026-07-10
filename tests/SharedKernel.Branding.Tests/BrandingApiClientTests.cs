using System.Net;
using SharedKernel.HttpClients;
using SharedKernel.Testing;

namespace SharedKernel.Branding.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.ApiClientCapability)]
public sealed class BrandingApiClientTests
{
    private const string PublicSettingsPath = $"/api/v1/{BrandingRoutes.PublicSettingsPath}";
    private const string ManagementSettingsPath = $"/api/v1/{BrandingRoutes.ManagementSettingsPath}";

    [Fact]
    public async Task GetPublicSettings_reads_public_branding_settings()
    {
        // Arrange
        using var handler = new BrandingApiClientTestHandler(HttpStatusCode.OK, """
            {
              "brandName": "Example Brand",
              "primaryColor": "#112233",
              "accentColor": "#445566",
              "backgroundColor": "#FFFFFF",
              "textColor": "#000000",
              "headingFontFamily": "Inter",
              "bodyFontFamily": "Source Serif 4",
              "logoUri": "/assets/logo.svg"
            }
            """);
        using var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://branding.example.test"),
        };
        var sut = new BrandingApiClient(httpClient);

        // Act
        var settings = await sut.GetPublicSettings(TestContext.Current.CancellationToken);

        // Assert
        handler.LastMethod.ShouldBe(HttpMethod.Get);
        handler.LastPath.ShouldBe(PublicSettingsPath);
        settings.BrandName.ShouldBe("Example Brand");
        settings.LogoUri.ShouldBe("/assets/logo.svg");
    }

    [Fact]
    public async Task GetSettings_reads_management_branding_settings()
    {
        // Arrange
        using var handler = new BrandingApiClientTestHandler(HttpStatusCode.OK, """
            {
              "brandName": "Managed Brand",
              "primaryColor": "#112233",
              "accentColor": "#445566",
              "backgroundColor": "#FFFFFF",
              "textColor": "#000000",
              "headingFontFamily": "Inter",
              "bodyFontFamily": "Source Serif 4",
              "logoUri": "https://cdn.example.test/logo.svg"
            }
            """);
        using var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://branding.example.test"),
        };
        var sut = new BrandingApiClient(httpClient);

        // Act
        var settings = await sut.GetSettings(TestContext.Current.CancellationToken);

        // Assert
        handler.LastMethod.ShouldBe(HttpMethod.Get);
        handler.LastPath.ShouldBe(ManagementSettingsPath);
        settings.BrandName.ShouldBe("Managed Brand");
        settings.LogoUri.ShouldBe("https://cdn.example.test/logo.svg");
    }

    [Fact]
    public async Task SaveSettings_puts_management_settings_and_reads_response()
    {
        // Arrange
        using var handler = new BrandingApiClientTestHandler(HttpStatusCode.OK, """
            {
              "brandName": "Saved Brand",
              "primaryColor": "#112233",
              "accentColor": "#445566",
              "backgroundColor": "#FFFFFF",
              "textColor": "#000000",
              "headingFontFamily": "Inter",
              "bodyFontFamily": "Source Serif 4",
              "logoUri": null
            }
            """);
        using var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://branding.example.test"),
        };
        var sut = new BrandingApiClient(httpClient);
        var request = BrandingSettingsTestData.ValidRequest();

        // Act
        var settings = await sut.SaveSettings(request, TestContext.Current.CancellationToken);

        // Assert
        handler.LastMethod.ShouldBe(HttpMethod.Put);
        handler.LastPath.ShouldBe(ManagementSettingsPath);
        handler.LastRequestBody.ShouldContain("\"brandName\":\"Example Brand\"", StringComparison.Ordinal);
        settings.BrandName.ShouldBe("Saved Brand");
        settings.LogoUri.ShouldBeNull();
    }

    [Fact]
    public async Task SaveSettings_throws_contract_validation_exception_for_validation_problem()
    {
        // Arrange
        using var handler = new BrandingApiClientTestHandler(HttpStatusCode.BadRequest, """
            {
              "errors": {
                "BrandName": ["Brand name is required."]
              }
            }
            """);
        using var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://branding.example.test"),
        };
        var sut = new BrandingApiClient(httpClient);
        var request = BrandingSettingsTestData.ValidRequest();

        // Act
        ContractValidationException? exception = null;
        try
        {
            await sut.SaveSettings(request, TestContext.Current.CancellationToken);
        }
        catch (ContractValidationException caught)
        {
            exception = caught;
        }

        // Assert
        exception.ShouldNotBeNull();
        exception.ValidationErrors.ContainsKey(nameof(BrandingSettingsDto.BrandName)).ShouldBeTrue();
    }
}
