using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SharedKernel.Testing;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>
/// Tests the error pages: an unhandled exception and an empty error response are re-executed as the page for their status code in a new
/// scope, as a GET unless they were a HEAD, in the language of the request; a direct request to an error page is not found.
/// </summary>
[TestScope(TestScope.Integration)]
public sealed class ErrorPagesTests
{
    [Theory]
    [InlineData("/fail", HttpStatusCode.InternalServerError, "page 500 new scope")]
    [InlineData("/missing", HttpStatusCode.NotFound, "page 404 new scope")]
    public async Task A_failed_request_is_re_executed_as_the_page_for_its_status_in_a_new_scope(string path, HttpStatusCode status, string page)
    {
        // Arrange
        await using var host = await ErrorPagesTestHost.Start(localized: false);

        // Act
        using var response = await host.Send(HttpMethod.Get, path);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(status);
        body.ShouldBe(page);
    }

    [Theory]
    [InlineData("/fail", HttpStatusCode.InternalServerError)]
    [InlineData("/missing", HttpStatusCode.NotFound)]
    public async Task A_failed_post_renders_the_page_as_a_get(string path, HttpStatusCode status)
    {
        // Arrange
        await using var host = await ErrorPagesTestHost.Start(localized: false);

        // Act
        using var response = await host.Send(HttpMethod.Post, path);

        // Assert
        response.StatusCode.ShouldBe(status);
        response.Headers.GetValues(ErrorPagesTestHost.MethodHeader).ShouldBe([HttpMethods.Get]);
    }

    [Theory]
    [InlineData("/fail", HttpStatusCode.InternalServerError)]
    [InlineData("/missing", HttpStatusCode.NotFound)]
    public async Task A_failed_head_stays_a_head(string path, HttpStatusCode status)
    {
        // Arrange
        await using var host = await ErrorPagesTestHost.Start(localized: false);

        // Act
        using var response = await host.Send(HttpMethod.Head, path);
        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(status);
        response.Headers.GetValues(ErrorPagesTestHost.MethodHeader).ShouldBe([HttpMethods.Head]);
        body.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/error")]
    [InlineData("/error/500")]
    [InlineData("/ERROR/500")]
    [InlineData("/not-found")]
    public async Task A_direct_request_to_an_error_page_is_not_found(string path)
    {
        // Arrange
        await using var host = await ErrorPagesTestHost.Start(localized: false);

        // Act
        using var response = await host.Send(HttpMethod.Get, path);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.ShouldBe("page 404 new scope");
    }

    [Theory]
    [InlineData("/fail", "pt-BR")]
    [InlineData("/fail?culture=en", "en")]
    [InlineData("/missing?culture=en", "en")]
    public async Task The_page_names_the_language_of_the_request(string path, string language)
    {
        // Arrange
        await using var host = await ErrorPagesTestHost.Start(localized: true);

        // Act
        using var response = await host.Send(HttpMethod.Get, path);

        // Assert
        response.Content.Headers.ContentLanguage.ShouldBe([language]);
    }

    [Fact]
    public async Task Without_request_localization_the_page_names_no_language()
    {
        // Arrange
        await using var host = await ErrorPagesTestHost.Start(localized: false);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/fail");

        // Assert
        response.Content.Headers.ContentLanguage.ShouldBeEmpty();
    }

    [Fact]
    public void Arguments_are_checked()
    {
        // Arrange
        using var app = WebApplication.CreateBuilder().Build();
        var withoutApp = () => ErrorPageExtensions.UseErrorPages(null!, "/error");
        var emptyPrefix = () => app.UseErrorPages(PathString.Empty);
        var rootPrefix = () => app.UseErrorPages("/");
        var nullHidden = () => app.UseErrorPages("/error", null!);
        var rootHidden = () => app.UseErrorPages("/error", "/not-found", "/");

        // Act & Assert
        withoutApp.ShouldThrow<ArgumentNullException>();
        emptyPrefix.ShouldThrow<ArgumentException>();
        rootPrefix.ShouldThrow<ArgumentException>();
        nullHidden.ShouldThrow<ArgumentNullException>();
        rootHidden.ShouldThrow<ArgumentException>();
    }
}
