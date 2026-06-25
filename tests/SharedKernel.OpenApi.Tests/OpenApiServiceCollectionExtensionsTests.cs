using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        var exception = Assert.Throws<TargetInvocationException>(() => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(null, ["tours"]));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public void Throws_When_Boundary_Names_Are_Null()
    {
        var services = OpenApiTestServiceCollectionFactory.Create();

        var exception = Assert.Throws<TargetInvocationException>(() => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(services, null));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public void Throws_When_A_Boundary_Name_Is_Whitespace()
    {
        var services = OpenApiTestServiceCollectionFactory.Create();

        var exception = Assert.Throws<TargetInvocationException>(() => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(services, ["tours", " "]));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void Throws_When_Boundary_Names_Contain_Duplicates()
    {
        var services = OpenApiTestServiceCollectionFactory.Create();

        var exception = Assert.Throws<TargetInvocationException>(() => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(services, ["tours", "Tours"]));

        Assert.IsType<ArgumentException>(exception.InnerException);
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
        var document = await OpenApiDocumentFactory.CreateDocumentFromApplication("tours", app =>
        {
            app.MapGroup("/tours")
                .WithGroupName("tours")
                .WithTags("tours")
                .MapGet("/", () => TypedResults.Ok());
            app.MapGroup("/tours-archive")
                .WithGroupName("tours-archive")
                .WithTags("tours-archive")
                .MapGet("/", () => TypedResults.Ok());
        });

        // Assert
        Assert.Contains("/tours", document.Paths.Keys);
        Assert.DoesNotContain("/tours-archive", document.Paths.Keys);
    }

    private static class OpenApiServiceCollectionExtensionsTestsHelpers
    {
        public static void InvokeAddBoundaryOpenApiDocuments(IServiceCollection? services, IReadOnlyCollection<string>? boundaryNames)
        {
            var method = typeof(OpenApiServiceCollectionExtensions).GetMethod(nameof(OpenApiServiceCollectionExtensions.AddBoundaryOpenApiDocuments))
                ?? throw new InvalidOperationException("Could not locate AddBoundaryOpenApiDocuments.");

            _ = method.Invoke(null, [services, boundaryNames]);
        }
    }
}
