using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>
/// Tests the status-code pages that re-execute every empty error response as a page, except under the excluded path prefixes, whose
/// answers stay as the endpoints wrote them.
/// </summary>
public sealed class StatusCodePagesExclusionTests
{
    [Theory]
    [InlineData("/missing")]
    [InlineData("/items")]
    [InlineData("/apiary/items")]
    public async Task An_empty_error_outside_the_excluded_prefixes_is_re_executed_as_the_page(string path)
    {
        // Arrange
        await using var host = await StatusCodePagesTestHost.Start(createScope: true);

        // Act
        using var response = await host.Send(HttpMethod.Get, path);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.ShouldBe("page 404 new scope");
    }

    [Theory]
    [InlineData("/api/missing")]
    [InlineData("/api/items")]
    [InlineData("/API/items")]
    [InlineData("/health")]
    public async Task An_empty_error_under_an_excluded_prefix_keeps_its_empty_answer(string path)
    {
        // Arrange
        await using var host = await StatusCodePagesTestHost.Start(createScope: true);

        // Act
        using var response = await host.Send(HttpMethod.Get, path);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_problem_answer_under_an_excluded_prefix_is_kept()
    {
        // Arrange
        await using var host = await StatusCodePagesTestHost.Start(createScope: true);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/api/conflict");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task The_page_runs_in_the_scope_of_the_failed_request_when_no_scope_is_created()
    {
        // Arrange
        await using var host = await StatusCodePagesTestHost.Start(createScope: false);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/items");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.ShouldBe("page 404 same scope");
    }

    [Fact]
    public void Arguments_are_checked()
    {
        // Arrange
        using var app = WebApplication.CreateBuilder().Build();
        var withoutApp = () => StatusCodePagesExclusionExtensions.UseStatusCodePagesWithReExecuteExcept(null!, "/status/{0}", true, "/api");
        var blankPath = () => app.UseStatusCodePagesWithReExecuteExcept(" ", true, "/api");
        var nullPrefixes = () => app.UseStatusCodePagesWithReExecuteExcept("/status/{0}", true, null!);
        var noPrefix = () => app.UseStatusCodePagesWithReExecuteExcept("/status/{0}", true);
        var emptyPrefix = () => app.UseStatusCodePagesWithReExecuteExcept("/status/{0}", true, "/api", PathString.Empty);
        var rootPrefix = () => app.UseStatusCodePagesWithReExecuteExcept("/status/{0}", true, "/");

        // Act & Assert
        withoutApp.ShouldThrow<ArgumentNullException>();
        blankPath.ShouldThrow<ArgumentException>();
        nullPrefixes.ShouldThrow<ArgumentNullException>();
        noPrefix.ShouldThrow<ArgumentException>().ParamName.ShouldBe("excludedPrefixes");
        emptyPrefix.ShouldThrow<ArgumentException>().ParamName.ShouldBe("excludedPrefixes");
        rootPrefix.ShouldThrow<ArgumentException>().ParamName.ShouldBe("excludedPrefixes");
    }
}
