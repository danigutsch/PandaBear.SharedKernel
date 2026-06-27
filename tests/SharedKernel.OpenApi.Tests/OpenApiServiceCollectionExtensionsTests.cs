using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using Xunit;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Verifies named document registration and route filtering behavior.
/// </summary>
public sealed class OpenApiServiceCollectionExtensionsTests
{
    [Fact]
    public void Throws_when_services_are_null()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(null, ["tours"]));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public void Throws_when_boundary_names_are_null()
    {
        var services = OpenApiTestServiceCollectionFactory.Create();

        var exception = Assert.Throws<TargetInvocationException>(() => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(services, null));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public void Throws_when_a_boundary_name_is_whitespace()
    {
        var services = OpenApiTestServiceCollectionFactory.Create();

        var exception = Assert.Throws<TargetInvocationException>(() => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(services, ["tours", " "]));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void Throws_when_boundary_names_contain_duplicates()
    {
        var services = OpenApiTestServiceCollectionFactory.Create();

        var exception = Assert.Throws<TargetInvocationException>(() => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(services, ["tours", "Tours"]));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public async Task Includes_exact_boundary_and_nested_paths_only()
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
    public async Task Excludes_prefix_like_paths_from_named_document()
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

}
