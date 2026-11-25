using System.Net;
using SharedKernel.Blazor.Site.Tests.CulturePages;
using SharedKernel.Testing;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>
/// Tests the base of static per-culture pages through a real host: the culture segment picks the language and its content, and an
/// unsupported culture, a missing item or a fixed section under another language's segment answers 404.
/// </summary>
[TestScope(TestScope.Integration)]
public sealed class CulturePageBaseTests
{
    [Theory]
    [InlineData("/pt/culture-sample", "pt")]
    [InlineData("/en/culture-sample", "en")]
    [InlineData("/EN/culture-sample", "en")]
    public async Task A_supported_culture_renders_the_content_of_that_language(string path, string culture)
    {
        // Arrange
        await using var host = await CulturePagesHost.Start();

        // Act
        var (status, body) = await host.Get(path);

        // Assert
        status.ShouldBe(HttpStatusCode.OK);
        body.ShouldContain($"language={culture};content={culture};", StringComparison.Ordinal);
        body.ShouldContain("method=GET</p>", StringComparison.Ordinal);
        host.Reader.Cultures.Select(static read => read.Value).ShouldBe([culture]);
    }

    [Theory]
    [InlineData("/de/culture-sample")]
    [InlineData("/pt-BR/culture-sample")]
    [InlineData("/x/culture-sample")]
    public async Task An_unsupported_culture_answers_not_found_without_reading_content(string path)
    {
        // Arrange
        await using var host = await CulturePagesHost.Start();

        // Act
        var (status, body) = await host.Get(path);

        // Assert
        status.ShouldBe(HttpStatusCode.NotFound);
        body.ShouldNotContain("language=", StringComparison.Ordinal);
        host.Reader.Cultures.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_page_whose_item_is_missing_answers_not_found()
    {
        // Arrange
        await using var host = await CulturePagesHost.Start();

        // Act
        var (status, body) = await host.Get("/en/culture-missing");

        // Assert
        status.ShouldBe(HttpStatusCode.NotFound);
        body.ShouldNotContain("found", StringComparison.Ordinal);
        host.Reader.Cultures.Select(static read => read.Value).ShouldBe(["en"]);
    }

    [Theory]
    [InlineData("/pt/viagens", HttpStatusCode.OK)]
    [InlineData("/en/tours", HttpStatusCode.OK)]
    [InlineData("/pt/viagens/", HttpStatusCode.OK)]
    [InlineData("/en/tours?page=2", HttpStatusCode.OK)]
    [InlineData("/en/TOURS", HttpStatusCode.OK)]
    [InlineData("/pt/tours", HttpStatusCode.NotFound)]
    [InlineData("/en/viagens", HttpStatusCode.NotFound)]
    public async Task A_fixed_section_answers_only_under_the_segment_of_its_own_language(string path, HttpStatusCode expected)
    {
        // Arrange
        await using var host = await CulturePagesHost.Start();

        // Act
        var (status, body) = await host.Get(path);

        // Assert
        status.ShouldBe(expected);
        body.Contains("section", StringComparison.Ordinal).ShouldBe(expected == HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/pt/culture-sample", "/pt/culture-sample")]
    [InlineData("/pt/culture-sample/", "/pt/culture-sample")]
    [InlineData("/pt/culture-sample?page=2", "/pt/culture-sample")]
    public async Task The_current_path_has_no_query_and_no_trailing_slash(string path, string expected)
    {
        // Arrange
        await using var host = await CulturePagesHost.Start();

        // Act
        var (_, body) = await host.Get(path);

        // Assert
        body.ShouldContain($"path={expected};", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_content_is_read_with_the_token_of_the_request()
    {
        // Arrange
        await using var host = await CulturePagesHost.Start();

        // Act
        await host.Get("/pt/culture-sample");

        // Assert
        host.Reader.Cultures.ShouldNotBeEmpty();
        host.Reader.ReadWithCancellableToken.ShouldBeTrue();
    }
}
