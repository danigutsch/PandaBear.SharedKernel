using SharedKernel.Testing;

namespace SharedKernel.Branding.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.ValidationCapability)]
public sealed class BrandingSettingsTests
{
    [Fact]
    public void Create_returns_validated_settings_when_request_is_valid()
    {
        // Arrange
        var request = BrandingSettingsTestData.ValidRequest();

        // Act
        var result = BrandingSettings.Create(request, BrandingSettingsTestData.AllowedFontFamilies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var settings = result.Value;
        settings.BrandName.ShouldBe("Example Brand");
        settings.PrimaryColor.ShouldBe("#112233");
        settings.AccentColor.ShouldBe("#AABBCC");
        settings.HeadingFontFamily.ShouldBe("Inter");
        settings.BodyFontFamily.ShouldBe("Source Serif 4");
        settings.LogoUri.ShouldBe("/assets/logo.svg");
    }

    [Fact]
    public void Create_sanitizes_brand_name_and_round_trips_to_dto()
    {
        // Arrange
        var request = BrandingSettingsTestData.ValidRequest();
        request.BrandName = "  Example\t\tBrand\0  ";

        // Act
        var result = BrandingSettings.Create(request, BrandingSettingsTestData.AllowedFontFamilies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var dto = result.Value.ToDto();
        dto.BrandName.ShouldBe("Example Brand");
        dto.PrimaryColor.ShouldBe("#112233");
        dto.LogoUri.ShouldBe("/assets/logo.svg");
    }

    [Fact]
    public void Create_rejects_missing_brand_name()
    {
        // Arrange
        var request = BrandingSettingsTestData.ValidRequest();
        request.BrandName = "   ";

        // Act
        var result = BrandingSettings.Create(request, BrandingSettingsTestData.AllowedFontFamilies);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.ContainsKey(nameof(BrandingSettingsDto.BrandName)).ShouldBeTrue();
    }

    [Fact]
    public void Create_rejects_too_long_brand_name()
    {
        // Arrange
        var request = BrandingSettingsTestData.ValidRequest();
        request.BrandName = new string('A', BrandingContractConstants.MaxBrandNameLength + 1);

        // Act
        var result = BrandingSettings.Create(request, BrandingSettingsTestData.AllowedFontFamilies);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.ContainsKey(nameof(BrandingSettingsDto.BrandName)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#12345G")]
    [InlineData("rgb(1,2,3)")]
    [InlineData("#112233;background:url(javascript:alert(1))")]
    public void Create_rejects_unsafe_colors(string color)
    {
        // Arrange
        var request = BrandingSettingsTestData.ValidRequest();
        request.PrimaryColor = color;

        // Act
        var result = BrandingSettings.Create(request, BrandingSettingsTestData.AllowedFontFamilies);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.ContainsKey(nameof(BrandingSettingsDto.PrimaryColor)).ShouldBeTrue();
    }

    [Fact]
    public void Create_rejects_fonts_outside_allow_list()
    {
        // Arrange
        var request = BrandingSettingsTestData.ValidRequest();
        request.HeadingFontFamily = "Comic Sans MS";

        // Act
        var result = BrandingSettings.Create(request, BrandingSettingsTestData.AllowedFontFamilies);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.ContainsKey(nameof(BrandingSettingsDto.HeadingFontFamily)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://cdn.example.test/logo.svg", "https://cdn.example.test/logo.svg")]
    [InlineData("  /images/logo.svg  ", "/images/logo.svg")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void Create_allows_optional_root_relative_or_https_logo_uri(string? logoValue, string? expected)
    {
        // Arrange
        var request = BrandingSettingsTestData.ValidRequest();
        request.LogoUri = logoValue;

        // Act
        var result = BrandingSettings.Create(request, BrandingSettingsTestData.AllowedFontFamilies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.LogoUri.ShouldBe(expected);
    }

    [Theory]
    [InlineData("http://example.test/logo.svg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("//cdn.example.test/logo.svg")]
    [InlineData("https://user:password@cdn.example.test/logo.svg")]
    [InlineData("https://cdn.example.test\\logo.svg")]
    [InlineData("logo.svg")]
    [InlineData("/images/my logo.svg")]
    [InlineData("/images\\logo.svg")]
    public void Create_rejects_unsafe_logo_uri(string logoValue)
    {
        // Arrange
        var request = BrandingSettingsTestData.ValidRequest();
        request.LogoUri = logoValue;

        // Act
        var result = BrandingSettings.Create(request, BrandingSettingsTestData.AllowedFontFamilies);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.ContainsKey(nameof(BrandingSettingsDto.LogoUri)).ShouldBeTrue();
    }
}
