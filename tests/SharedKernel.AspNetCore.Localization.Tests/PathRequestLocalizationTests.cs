using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SharedKernel.Testing;
using SharedKernel.BuildingBlocks;
using static SharedKernel.AspNetCore.Localization.Tests.LocalizationTestCultures;

namespace SharedKernel.AspNetCore.Localization.Tests;

/// <summary>
/// Tests the request localization from the culture segment of the path: the request's culture and UI culture, and its
/// <c>Content-Language</c>, are the language tag the segment's culture declares, or the default for any other path.
/// </summary>
[TestScope(TestScope.Integration)]
public sealed class PathRequestLocalizationTests
{
    [Theory]
    [InlineData("/pt/tours", "pt-BR")]
    [InlineData("/en/tours", "en")]
    [InlineData("/fr/tours", "pt-BR")]
    [InlineData("/", "pt-BR")]
    public async Task The_request_is_in_the_language_tag_of_its_culture_segment(string path, string language)
    {
        // Arrange
        await using var app = await PathRequestLocalizationTestApp.Start(static app => app.UsePathRequestLocalization(LanguageTags));

        // Act
        var context = await PathRequestLocalizationTestApp.Send(app, path);

        // Assert
        context.Response.Headers[PathRequestLocalizationTestApp.CultureHeader].ToString().ShouldBe($"{language}|{language}");
        context.Response.Headers.ContentLanguage.ToString().ShouldBe(language);
    }

    [Fact]
    public async Task Only_the_path_decides_the_language()
    {
        // Arrange
        await using var app = await PathRequestLocalizationTestApp.Start(static app => app.UsePathRequestLocalization(LanguageTags));

        // Act
        var context = await PathRequestLocalizationTestApp.Send(app, "/pt/tours", static request =>
        {
            request.Headers.AcceptLanguage = "en";
            request.QueryString = new QueryString("?culture=en");
        });

        // Assert
        context.Response.Headers.ContentLanguage.ToString().ShouldBe("pt-BR");
    }

    [Theory]
    [InlineData("/pt/tours", "pt")]
    [InlineData("/en/tours", "en")]
    [InlineData("/tours", "pt")]
    public async Task Without_language_tags_the_request_is_in_its_culture_segment(string path, string language)
    {
        // Arrange
        await using var app = await PathRequestLocalizationTestApp.Start(static app => app.UsePathRequestLocalization(Supported));

        // Act
        var context = await PathRequestLocalizationTestApp.Send(app, path);

        // Assert
        context.Response.Headers.ContentLanguage.ToString().ShouldBe(language);
    }

    [Fact]
    public void Arguments_are_checked()
    {
        // Arrange
        using var app = WebApplication.CreateBuilder().Build();
        var withoutAppTags = () => PathRequestLocalizationExtensions.UsePathRequestLocalization(null!, LanguageTags);
        var withoutAppCultures = () => PathRequestLocalizationExtensions.UsePathRequestLocalization(null!, Supported);
        var withoutTags = () => app.UsePathRequestLocalization((CultureLanguageTags)null!);
        var withoutCultures = () => app.UsePathRequestLocalization((SupportedCultures)null!);

        // Act & Assert
        withoutAppTags.ShouldThrow<ArgumentNullException>();
        withoutAppCultures.ShouldThrow<ArgumentNullException>();
        withoutTags.ShouldThrow<ArgumentNullException>();
        withoutCultures.ShouldThrow<ArgumentNullException>();
    }
}
