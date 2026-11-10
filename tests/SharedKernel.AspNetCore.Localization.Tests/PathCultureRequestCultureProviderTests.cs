using Microsoft.AspNetCore.Http;
using static PandaBear.AspNetCore.Localization.Tests.LocalizationTestCultures;

namespace PandaBear.AspNetCore.Localization.Tests;

/// <summary>Tests <see cref="PathCultureRequestCultureProvider" />.</summary>
public sealed class PathCultureRequestCultureProviderTests
{
    [Theory]
    [InlineData("/pt/tours", "pt")]
    [InlineData("/EN/tours", "en")]
    [InlineData("/en", "en")]
    public async Task Reads_the_culture_from_the_first_path_segment(string path, string expected)
    {
        // Arrange
        var provider = new PathCultureRequestCultureProvider(Supported);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        // Act
        var result = await provider.DetermineProviderCultureResult(context);

        // Assert
        var culture = result.ShouldNotBeNull();
        culture.Cultures.Select(static c => c.Value).ShouldBe([expected]);
        culture.UICultures.Select(static c => c.Value).ShouldBe([expected]);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/fr/tours")]
    [InlineData("/tours/pt")]
    public async Task Returns_no_result_without_a_supported_prefix(string path)
    {
        // Arrange
        var provider = new PathCultureRequestCultureProvider(Supported);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        // Act
        var result = await provider.DetermineProviderCultureResult(context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Rejects_null_supported_cultures()
    {
        // Act
        var create = () => new PathCultureRequestCultureProvider(null!);

        // Assert
        create.ShouldThrow<ArgumentNullException>();
    }
}
