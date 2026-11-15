using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.TestHost;
using PandaBear.BuildingBlocks;
using static PandaBear.AspNetCore.Localization.Tests.LocalizationTestCultures;

namespace PandaBear.AspNetCore.Localization.Tests;

/// <summary>Tests <see cref="PathCultureRequestCultureProvider" />.</summary>
public sealed class PathCultureRequestCultureProviderTests
{
    [Theory]
    [InlineData("/pt/tours", "pt")]
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
    [InlineData("/EN/tours")]
    [InlineData("/ pt/tours")]
    public async Task Returns_no_result_without_a_canonical_supported_prefix(string path)
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
        var create = () => new PathCultureRequestCultureProvider((SupportedCultures)null!);

        // Assert
        create.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData("/pt/tours", "pt-BR")]
    [InlineData("/pt", "pt-BR")]
    [InlineData("/en/tours", "en")]
    public async Task Reads_the_language_tag_of_the_culture_in_the_first_path_segment(string path, string expected)
    {
        // Arrange
        var provider = new PathCultureRequestCultureProvider(LanguageTags);
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
    [InlineData("/pt-BR/tours")]
    [InlineData("/PT/tours")]
    [InlineData("/tours/pt")]
    public async Task Returns_no_result_with_language_tags_when_the_first_segment_is_not_a_canonical_culture(string path)
    {
        // Arrange
        var provider = new PathCultureRequestCultureProvider(LanguageTags);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        // Act
        var result = await provider.DetermineProviderCultureResult(context);

        // Assert
        result.ShouldBeNull("the address segment stays the culture; a language tag is not an address segment");
    }

    [Fact]
    public void Rejects_null_language_tags()
    {
        // Act
        var create = () => new PathCultureRequestCultureProvider((CultureLanguageTags)null!);

        // Assert
        create.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData("/pt/tours", "pt-BR", true)]
    [InlineData("/en/tours", "en", true)]
    [InlineData("/", "pt-BR", false)]
    public async Task Request_localization_uses_the_language_tag_as_the_request_culture(string path, string expected, bool fromPath)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        string[] languageTags = [.. LanguageTags.LanguageTags.Select(static tag => tag.Value)];
        app.UseRequestLocalization(options =>
        {
            options.SetDefaultCulture(LanguageTags.DefaultLanguageTag.Value)
                .AddSupportedCultures(languageTags)
                .AddSupportedUICultures(languageTags);
            options.ApplyCurrentCultureToResponseHeaders = true;
            options.RequestCultureProviders = [new PathCultureRequestCultureProvider(LanguageTags)];
        });
        app.Run(static context => context.Response.WriteAsync("ok"));
        await app.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var context = await app.GetTestServer().SendAsync(request => request.Request.Path = path, TestContext.Current.CancellationToken);

        // Assert
        var feature = context.Features.Get<IRequestCultureFeature>().ShouldNotBeNull();
        var decidedByPath = feature.Provider is PathCultureRequestCultureProvider;
        decidedByPath.ShouldBe(fromPath, "the path decides a culture only when its first segment is a supported culture");
        var requestCulture = feature.RequestCulture;
        requestCulture.Culture.Name.ShouldBe(expected, "formatting follows the language tag, not the parent of the address segment");
        requestCulture.UICulture.Name.ShouldBe(expected);
        context.Response.Headers.ContentLanguage.ToString().ShouldBe(expected);
    }
}
