using System.Net;
using PandaBear.Testing.Http;
using Xunit.Sdk;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the Created and Location assertions.</summary>
public sealed class HttpCreatedLocationAssertionTests
{
    [Fact]
    public async Task Created_requires_201_and_a_location_header()
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.Created);
        response.Headers.Location = new Uri("/api/tours/7", UriKind.Relative);

        // Act
        var result = await response.ShouldBeCreated(SuccessResponses.Token);

        // Assert
        result.ShouldBeSameAs(response);
    }

    [Fact]
    public async Task Created_without_a_location_header_fails()
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.Created);
        Func<Task> assertion = async () => await response.ShouldBeCreated(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected a Location header but the response has none.");
    }

    [Fact]
    public async Task Created_with_another_status_fails_before_the_location_is_checked()
    {
        // Arrange
        using var response = SuccessResponses.Text(HttpStatusCode.OK, "done", "text/plain");
        Func<Task> assertion = async () => await response.ShouldBeCreated(SuccessResponses.Token);

        // Act
        var failure = await assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe("Expected HTTP status 201 (Created) but was 200 (OK). Body: done");
    }

    [Theory]
    [InlineData("/api/tours/7", UriKind.Relative)]
    [InlineData("https://example.test/api/tours/7", UriKind.Absolute)]
    [InlineData("https://example.test/api/tours/7?page=2#top", UriKind.Absolute)]
    public void Location_with_the_path_prefix_returns_the_uri(string location, UriKind kind)
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.Created);
        response.Headers.Location = new Uri(location, kind);

        // Act
        var uri = response.ShouldHaveLocation("/api/tours/");

        // Assert
        uri.ShouldBeSameAs(response.Headers.Location);
    }

    [Theory]
    [InlineData("/other/7?token=secret", UriKind.Relative, "/other/7")]
    [InlineData("https://example.test/other/7?token=secret#frag", UriKind.Absolute, "/other/7")]
    [InlineData("/API/tours/7", UriKind.Relative, "/API/tours/7")]
    public void Location_with_another_path_fails_without_quoting_the_query_or_fragment(string location, UriKind kind, string reportedPath)
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.Created);
        response.Headers.Location = new Uri(location, kind);
        Action assertion = () => response.ShouldHaveLocation("/api/tours/");

        // Act
        var failure = assertion.ShouldThrow<XunitException>();

        // Assert
        failure.Message.ShouldBe($"Expected the Location path to start with '/api/tours/' but was '{reportedPath}'.");
    }

    [Fact]
    public void Location_missing_fails_and_arguments_are_validated()
    {
        // Arrange
        using var response = SuccessResponses.Empty(HttpStatusCode.Created);
        HttpResponseMessage missing = null!;
        Action withoutLocation = () => response.ShouldHaveLocation("/api/tours/");
        Action nullResponse = () => missing.ShouldHaveLocation("/api/tours/");
        Action nullPrefix = () => response.ShouldHaveLocation(null!);

        // Act
        var failure = withoutLocation.ShouldThrow<XunitException>();
        nullResponse.ShouldThrow<ArgumentNullException>();
        nullPrefix.ShouldThrow<ArgumentNullException>();

        // Assert
        failure.Message.ShouldBe("Expected a Location header but the response has none.");
    }
}
