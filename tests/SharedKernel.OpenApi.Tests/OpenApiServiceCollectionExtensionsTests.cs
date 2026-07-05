using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.ApiVersioning;
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

    [Fact]
    public async Task Includes_matching_api_version_paths_only()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateApiVersionDocument(
            new ApiVersionDefinition(new ApiVersion(1)),
            app =>
            {
                app.MapGroup("/api/v1")
                    .WithGroupName("v1")
                    .WithTags("v1")
                    .MapGet("/status", () => TypedResults.Ok());
                app.MapGroup("/api/v2")
                    .WithGroupName("v2")
                    .WithTags("v2")
                    .MapGet("/status", () => TypedResults.Ok());
            });

        // Assert
        document.Info.Version.ShouldBe("1.0");
        document.Paths.Keys.ShouldContain("/api/v1/status");
        document.Paths.Keys.ShouldNotContain("/api/v2/status");
    }

    [Fact]
    public async Task Includes_matching_api_version_paths_with_normalized_route_prefix_only()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateApiVersionDocument(
            new ApiVersionDefinition(new ApiVersion(1, 1)),
            app =>
            {
                app.MapGroup("/admin-api/v1.1")
                    .WithGroupName("v1.1")
                    .WithTags("v1.1")
                    .MapGet("/status", () => TypedResults.Ok());
                app.MapGroup("/admin-api/v1")
                    .WithGroupName("v1")
                    .WithTags("v1")
                    .MapGet("/status", () => TypedResults.Ok());
            },
            " /admin-api/ ");

        // Assert
        document.Info.Version.ShouldBe("1.1");
        document.Paths.Keys.ShouldContain("/admin-api/v1.1/status");
        document.Paths.Keys.ShouldNotContain("/admin-api/v1/status");
    }

    [Fact]
    public void Throws_when_api_version_services_are_null()
    {
        // Arrange
        IServiceCollection? services = null;
        var version = new ApiVersionDefinition(new ApiVersion(1));

        // Act
        Action action = () => services!.AddApiVersionOpenApiDocuments([version]);

        // Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Throws_when_api_version_collection_is_null()
    {
        // Arrange
        var services = OpenApiTestServiceCollectionFactory.Create();
        IReadOnlyCollection<ApiVersionDefinition>? versions = null;

        // Act
        Action action = () => services.AddApiVersionOpenApiDocuments(versions!);

        // Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Throws_when_api_version_collection_contains_null()
    {
        // Arrange
        var services = OpenApiTestServiceCollectionFactory.Create();
        ApiVersionDefinition[] versions = [new ApiVersionDefinition(new ApiVersion(1)), null!];

        // Act
        Action action = () => services.AddApiVersionOpenApiDocuments(versions);

        // Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Throws_when_api_version_document_names_contain_duplicates()
    {
        // Arrange
        var services = OpenApiTestServiceCollectionFactory.Create();
        ApiVersionDefinition[] versions =
        [
            new ApiVersionDefinition(new ApiVersion(1)),
            new ApiVersionDefinition(new ApiVersion(1))
        ];

        // Act
        Action action = () => services.AddApiVersionOpenApiDocuments(versions);

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData("/")]
    [InlineData(" /// ")]
    public void Throws_when_api_version_route_prefix_normalizes_to_empty(string routePrefix)
    {
        var services = OpenApiTestServiceCollectionFactory.Create();
        var version = new ApiVersionDefinition(new ApiVersion(1));

        Action action = () => services.AddApiVersionOpenApiDocuments([version], routePrefix);

        var exception = action.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("routePrefix");
    }

}
