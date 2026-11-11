using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using PandaBear.HttpCaching.AspNetCore;

namespace PandaBear.AspNetCore.Tests;

/// <summary>
/// Tests the default public caching middleware: only an anonymous GET or HEAD answered 200 without its own cache header and without cookies
/// becomes publicly cacheable; every other response is left untouched.
/// </summary>
public sealed class DefaultPublicCachingTests
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task An_anonymous_ok_response_without_a_cache_header_becomes_public(string method)
    {
        // Arrange
        await using var host = await CachingTestHost.Start(MaxAge);

        // Act
        using var response = await host.Send(new HttpMethod(method), "/page");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull().ToString().ShouldBe("public, max-age=60");
    }

    [Fact]
    public async Task Another_method_is_left_untouched()
    {
        // Arrange
        await using var host = await CachingTestHost.Start(MaxAge);

        // Act
        using var response = await host.Send(HttpMethod.Post, "/page");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldBeNull();
    }

    [Theory]
    [InlineData("/missing")]
    [InlineData("/created")]
    [InlineData("/redirect")]
    public async Task A_status_other_than_200_is_left_untouched(string path)
    {
        // Arrange
        await using var host = await CachingTestHost.Start(MaxAge);

        // Act
        using var response = await host.Send(HttpMethod.Get, path);

        // Assert
        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldBeNull();
    }

    [Fact]
    public async Task A_response_with_its_own_cache_header_keeps_it()
    {
        // Arrange
        await using var host = await CachingTestHost.Start(MaxAge);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/own-cache");

        // Assert
        response.Headers.CacheControl.ShouldNotBeNull().ToString().ShouldBe("no-cache");
    }

    [Fact]
    public async Task A_response_that_sets_a_cookie_is_left_untouched()
    {
        // Arrange
        await using var host = await CachingTestHost.Start(MaxAge);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/cookie");

        // Assert
        response.Headers.Contains("Set-Cookie").ShouldBeTrue();
        response.Headers.CacheControl.ShouldBeNull();
    }

    [Fact]
    public async Task A_request_with_an_authorization_header_is_left_untouched()
    {
        // Arrange
        await using var host = await CachingTestHost.Start(MaxAge);

        // Act
        using var response = await host.Send(
            HttpMethod.Get,
            "/page",
            request => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldBeNull();
    }

    [Fact]
    public async Task A_request_of_an_authenticated_user_is_left_untouched()
    {
        // Arrange
        await using var host = await CachingTestHost.Start(MaxAge);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/page", request => request.Headers.Add(CachingTestHost.SignedInHeader, "yes"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task A_max_age_below_one_second_is_rejected(int milliseconds)
    {
        // Arrange
        await using var app = WebApplication.CreateBuilder().Build();

        // Act
        Action act = () => app.UseDefaultPublicCaching(TimeSpan.FromMilliseconds(milliseconds));

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_null_application_is_rejected()
    {
        // Act
        Action act = () => ApplicationBuilderHttpCacheExtensions.UseDefaultPublicCaching(null!, MaxAge);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
