using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Verifies named document registration and route filtering behavior.
/// </summary>
public sealed class OpenApiServiceCollectionExtensionsTests
{
    [Fact]
    public void Throws_When_Services_Are_Null()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeAddBoundaryOpenApiDocuments(null, ["tours"]));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public void Throws_When_Boundary_Names_Are_Null()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeAddBoundaryOpenApiDocuments(new ServiceCollection(), null));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public async Task Includes_Exact_Boundary_And_Nested_Paths_Only()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocument("tours", group =>
        {
            group.MapGet("/", () => TypedResults.Ok());
            group.MapGet("/{id:guid}", (Guid id) => TypedResults.Ok(id));
        });

        // Assert
        Assert.Contains("/tours", document.Paths.Keys);
        Assert.Contains("/tours/{id}", document.Paths.Keys);
    }

    [Fact]
    public async Task Excludes_Prefix_Like_Paths_From_Named_Document()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddBoundaryOpenApiDocuments(["tours"]);

        await using var app = builder.Build();
        app.MapGroup("/tours")
            .WithGroupName("tours")
            .WithTags("tours")
            .MapGet("/", () => TypedResults.Ok());
        app.MapGroup("/tours-archive")
            .WithGroupName("tours-archive")
            .WithTags("tours-archive")
            .MapGet("/", () => TypedResults.Ok());

        await app.StartAsync(cancellationToken);

        using var scope = app.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredKeyedService<IOpenApiDocumentProvider>("tours");

        // Act
        var document = await provider.GetOpenApiDocumentAsync(cancellationToken);

        // Assert
        Assert.Contains("/tours", document.Paths.Keys);
        Assert.DoesNotContain("/tours-archive", document.Paths.Keys);
    }

    private static void InvokeAddBoundaryOpenApiDocuments(IServiceCollection? services, IReadOnlyCollection<string>? boundaryNames)
    {
        var method = typeof(OpenApiServiceCollectionExtensions).GetMethod(nameof(OpenApiServiceCollectionExtensions.AddBoundaryOpenApiDocuments))
            ?? throw new InvalidOperationException("Could not locate AddBoundaryOpenApiDocuments.");

        _ = method.Invoke(null, [services, boundaryNames]);
    }
}
