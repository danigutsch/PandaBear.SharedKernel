using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace PandaBear.AspNetCore.Seo.Tests;

/// <summary>Characterizes the robots.txt endpoint that moved into the Seo package.</summary>
public sealed class RobotsTxtEndpointTests
{
    [Fact]
    public async Task Serves_the_application_owned_policy_as_plain_text()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.MapRobotsTxt("User-agent: *\nDisallow: /admin\n");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        // Act
        using var response = await client.GetAsync(new Uri("/robots.txt", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Content.Headers.ContentType.ShouldNotBeNull().ToString().ShouldBe("text/plain; charset=utf-8");
        body.ShouldBe("User-agent: *\nDisallow: /admin\n");
    }

    [Fact]
    public async Task Answers_head_with_the_headers_of_get()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.MapRobotsTxt("User-agent: *\nDisallow: /admin\n");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, new Uri("/robots.txt", UriKind.Relative));

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldNotBeNull().ToString().ShouldBe("text/plain; charset=utf-8");
        response.Content.Headers.ContentLength.ShouldBe(31);
    }

    [Fact]
    public void Rejects_blank_policy_text()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        // Act
        var map = () => app.MapRobotsTxt(" ");

        // Assert
        map.ShouldThrow<ArgumentException>();
    }
}
