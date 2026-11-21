using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using PandaBear.HttpCaching.AspNetCore;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Exercises snapshot-based stylesheet caching through the HTTP entry points.</summary>
public sealed class VersionedStylesheetTests
{
    private const string Css = ":root { --site-title: \"Oficina comunitária\"; }";
    private const string Version = "0123456789abcdef";

    [Theory]
    [InlineData("GET", "?v=0123456789abcdef", true)]
    [InlineData("HEAD", "?v=0123456789abcdef", true)]
    [InlineData("GET", "", false)]
    [InlineData("HEAD", "?v=stale", false)]
    [InlineData("GET", "?v=0123456789ABCDEF", false)]
    [InlineData("GET", "?v=0123456789abcdef&v=0123456789abcdef", false)]
    public async Task Only_the_exact_current_version_selects_immutable_caching(string method, string query, bool immutable)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            HttpCacheHeaders.SetPublic(context, TimeSpan.FromMinutes(1));
            await next(context);
        });
        app.MapVersionedStylesheet("/theme.css", _ => ValueTask.FromResult((Css, Version)));
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };
        using var request = new HttpRequestMessage(new HttpMethod(method), "/theme.css" + query);

        // Act
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldNotBeNull().ToString().ShouldBe("text/css; charset=utf-8");
        response.Content.Headers.ContentLength.ShouldBe(Encoding.UTF8.GetByteCount(Css));
        response.Headers.CacheControl.ShouldNotBeNull().ToString()
            .ShouldBe(immutable ? HttpCacheHeaders.ImmutableCacheControl : "public, max-age=60");
        body.ShouldBe(HttpMethods.IsHead(method) ? string.Empty : Css);
    }
}
