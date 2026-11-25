using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Verifies named endpoint paths through the real ASP.NET Core routing pipeline.</summary>
public sealed class NamedEndpointPathExtensionsTests
{
    [Theory]
    [InlineData("", "/items/42")]
    [InlineData("/catalog", "/catalog/items/42")]
    public async Task The_named_path_uses_explicit_values_and_the_request_mount(string pathBase, string expected)
    {
        // Arrange
        await using var host = await PipelineTestHost.Start(app =>
        {
            if (pathBase.Length > 0)
            {
                app.UsePathBase(pathBase);
            }

            app.MapGet("/items/{id:int}", (int id) => id).WithName("item");
            app.MapGet("/from/{id:int}", (HttpContext context, LinkGenerator routes) =>
                routes.GetRequiredPathByName(context, "item", new RouteValueDictionary { ["id"] = 42 }));
        });

        // Act
        var path = await host.Client.GetStringAsync(new Uri($"{pathBase}/from/7", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        path.ShouldBe(expected);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("item")]
    public async Task An_unavailable_path_throws_with_the_endpoint_name(string endpoint)
    {
        // Arrange
        await using var host = await PipelineTestHost.Start(app =>
        {
            app.MapGet("/items/{id:int}", (int id) => id).WithName("item");
            app.MapGet("/from/{id:int}", (HttpContext context, LinkGenerator routes) =>
            {
                var failure = Record.Exception(() => routes.GetRequiredPathByName(context, endpoint));
                return $"{failure?.GetType().Name} {failure?.Message}";
            });
        });

        // Act
        var failure = await host.Client.GetStringAsync(new Uri("/from/7", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        failure.ShouldBe($"InvalidOperationException Cannot generate a path to the named endpoint '{endpoint}'.");
    }
}
