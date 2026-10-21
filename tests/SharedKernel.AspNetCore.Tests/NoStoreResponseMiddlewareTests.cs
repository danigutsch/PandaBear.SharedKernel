using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using PandaBear.HttpCaching.AspNetCore;

namespace PandaBear.AspNetCore.Tests;

public sealed class NoStoreResponseMiddlewareTests
{
    [Theory]
    [InlineData("/protected/ok", 200, true)]
    [InlineData("/protected/rejected", 401, true)]
    [InlineData("/protected/forbidden", 403, true)]
    [InlineData("/protected/limited", 429, true)]
    [InlineData("/protected/missing", 404, true)]
    [InlineData("/protected/ok", 405, true)]
    [InlineData("/unrelated", 200, false)]
    [InlineData("/protected-other", 404, false)]
    public async Task Protects_scoped_responses_including_early_and_unmatched_responses(
        string path, int statusCode, bool protectedResponse)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.UseNoStoreResponses();
        app.Use(async (context, next) =>
        {
            var rejectedStatus = context.Request.Path.Value switch
            {
                "/protected/rejected" => StatusCodes.Status401Unauthorized,
                "/protected/forbidden" => StatusCodes.Status403Forbidden,
                "/protected/limited" => StatusCodes.Status429TooManyRequests,
                _ => 0
            };
            if (rejectedStatus != 0)
            {
                context.Response.StatusCode = rejectedStatus;
                return;
            }

            await next(context);
        });
        app.MapGroup("/protected").WithNoStoreResponses("/protected").MapGet("/ok", () => "ok");
        app.MapGet("/unrelated", () => "ok");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.ShouldHaveSingleItem()) };
        using var request = new HttpRequestMessage(
            statusCode == StatusCodes.Status405MethodNotAllowed ? HttpMethod.Post : HttpMethod.Get, path);

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe((HttpStatusCode)statusCode);
        var areEqual = response.Headers.CacheControl?.NoStore == true;
        areEqual.ShouldBe(protectedResponse);
    }
}
