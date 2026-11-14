using System.Net;
using Microsoft.AspNetCore.Builder;

namespace PandaBear.AspNetCore.Tests;

/// <summary>
/// Tests the permanent redirect middleware: GET and HEAD requests whose path the application moved get a 301 to a local path, and a target
/// that could leave the site, or that points back to the request, is never used.
/// </summary>
public sealed class PermanentRedirectTests
{
    [Fact]
    public async Task A_moved_path_gets_a_301_to_its_target_without_the_query()
    {
        // Arrange
        await using var host = await RedirectTestHost.Start(new PermanentRedirect("/new"));

        // Act
        using var response = await host.Send(HttpMethod.Get, "/old?ref=mail");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location.ShouldBe(new Uri("/new", UriKind.Relative));
    }

    [Fact]
    public async Task The_query_is_kept_when_the_resolver_asks_for_it()
    {
        // Arrange
        await using var host = await RedirectTestHost.Start(new PermanentRedirect("/new", PreserveQuery: true));

        // Act
        using var response = await host.Send(HttpMethod.Get, "/old?ref=mail");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location.ShouldBe(new Uri("/new?ref=mail", UriKind.Relative));
    }

    [Fact]
    public async Task A_head_request_is_redirected_too()
    {
        // Arrange
        await using var host = await RedirectTestHost.Start(new PermanentRedirect("/new"));

        // Act
        using var response = await host.Send(HttpMethod.Head, "/old");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location.ShouldBe(new Uri("/new", UriKind.Relative));
    }

    [Fact]
    public async Task The_path_base_is_kept_in_the_target()
    {
        // Arrange
        await using var host = await PipelineTestHost.Start(app =>
        {
            app.UsePathBase("/app");
            app.UsePermanentRedirects(_ => ValueTask.FromResult<PermanentRedirect?>(new PermanentRedirect("/new")));
        });

        // Act
        using var response = await host.Send(HttpMethod.Get, "/app/old");

        // Assert
        response.Headers.Location.ShouldBe(new Uri("/app/new", UriKind.Relative));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Other_methods_pass_through_without_asking_the_resolver(string method)
    {
        // Arrange
        var asked = 0;
        await using var host = await PipelineTestHost.Start(app =>
        {
            app.UsePermanentRedirects(_ =>
            {
                Interlocked.Increment(ref asked);
                return ValueTask.FromResult<PermanentRedirect?>(new PermanentRedirect("/new"));
            });
            app.MapMethods("/old", [method], () => "ok");
        });

        // Act
        using var response = await host.Send(new HttpMethod(method), "/old");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        asked.ShouldBe(0);
    }

    [Fact]
    public async Task A_path_without_a_target_passes_through()
    {
        // Arrange
        await using var host = await RedirectTestHost.Start(null);

        // Act
        using var response = await host.Send(HttpMethod.Get, "/old");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task A_target_equal_to_the_request_path_passes_through()
    {
        // Arrange
        await using var host = await RedirectTestHost.Start(new PermanentRedirect("/old", PreserveQuery: true));

        // Act
        using var response = await host.Send(HttpMethod.Get, "/old?ref=mail");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task A_percent_encoded_target_equal_to_the_encoded_request_path_passes_through()
    {
        // Arrange
        await using var host = await PipelineTestHost.Start(app =>
        {
            app.UsePermanentRedirects(_ => ValueTask.FromResult<PermanentRedirect?>(new PermanentRedirect("/caf%C3%A9")));
            app.MapGet("/café", () => "ok");
        });

        // Act
        using var response = await host.Send(HttpMethod.Get, "/caf%C3%A9");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task A_percent_encoded_path_base_stays_encoded_in_the_target()
    {
        // Arrange
        await using var host = await PipelineTestHost.Start(app =>
        {
            app.UsePathBase("/café");
            app.UsePermanentRedirects(_ => ValueTask.FromResult<PermanentRedirect?>(new PermanentRedirect("/new")));
        });

        // Act
        using var response = await host.Send(HttpMethod.Get, "/caf%C3%A9/old");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location.ShouldBe(new Uri("/caf%C3%A9/new", UriKind.Relative));
    }

    [Theory]
    [InlineData("")]
    [InlineData("new")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("\\\\evil.example")]
    [InlineData("/a\\b")]
    [InlineData("/a//b")]
    [InlineData("https://evil.example/")]
    [InlineData("javascript:alert(1)")]
    [InlineData(" /new")]
    [InlineData("/new\r\nSet-Cookie: a=b")]
    [InlineData("/new\u0000")]
    [InlineData("/new\u007F")]
    [InlineData("/new?x=1")]
    [InlineData("/new#top")]
    [InlineData("/café")]
    public async Task A_target_that_is_not_a_plain_local_path_is_ignored(string target)
    {
        // Arrange
        await using var host = await RedirectTestHost.Start(new PermanentRedirect(target));

        // Act
        using var response = await host.Send(HttpMethod.Get, "/old");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        // Arrange
        await using var app = WebApplication.CreateBuilder().Build();

        // Act
        Action noApp = () => PermanentRedirectExtensions.UsePermanentRedirects(null!, _ => ValueTask.FromResult<PermanentRedirect?>(null));
        Action noResolver = () => app.UsePermanentRedirects(null!);
        Action noPath = () => _ = new PermanentRedirect(null!);

        // Assert
        noApp.ShouldThrow<ArgumentNullException>();
        noResolver.ShouldThrow<ArgumentNullException>();
        noPath.ShouldThrow<ArgumentNullException>();
    }
}
