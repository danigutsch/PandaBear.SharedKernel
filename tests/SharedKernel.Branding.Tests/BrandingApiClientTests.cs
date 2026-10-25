using System.Net;
using PandaBear.Testing;

namespace PandaBear.Branding.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.ApiClientCapability)]
public sealed class BrandingApiClientTests
{
    private const string PublicSettingsPath = $"/api/v1/{BrandingRoutes.PublicSettingsPath}";

    [Fact]
    public async Task GetPublicSettings_reads_public_branding_settings()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """
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
        var request = handler.LastRequest.ShouldNotBeNull();
        request.Method.ShouldBe(HttpMethod.Get);
        request.PathAndQuery.ShouldBe(PublicSettingsPath);
        settings.BrandName.ShouldBe("Example Brand");
        settings.LogoUri.ShouldBe("/assets/logo.svg");
    }
}
