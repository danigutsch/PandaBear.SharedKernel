using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.ApiVersioning;
using System.Reflection;
using Xunit;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Verifies named document registration and route filtering behavior.
/// </summary>
[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.EndpointCategory)]
public sealed class OpenApiServiceCollectionExtensionsTests
{
    [Fact]
    public void Throws_when_services_are_null()
    {
        // Act
        Action action = () => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(null, ["tours"]);

        // Assert
        var exception = action.ShouldThrow<TargetInvocationException>();
        exception.InnerException.ShouldBeOfType<ArgumentNullException>();
    }

    [Fact]
    public void Throws_when_boundary_names_are_null()
    {
        // Arrange
        var services = OpenApiTestServiceCollectionFactory.Create();

        // Act
        Action action = () => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(services, null);

        // Assert
        var exception = action.ShouldThrow<TargetInvocationException>();
        exception.InnerException.ShouldBeOfType<ArgumentNullException>();
    }

    [Fact]
    public void Throws_when_a_boundary_name_is_whitespace()
    {
        // Arrange
        var services = OpenApiTestServiceCollectionFactory.Create();

        // Act
        Action action = () => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(services, ["tours", " "]);

        // Assert
        var exception = action.ShouldThrow<TargetInvocationException>();
        exception.InnerException.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public void Throws_when_boundary_names_contain_duplicates()
    {
        // Arrange
        var services = OpenApiTestServiceCollectionFactory.Create();

        // Act
        Action action = () => OpenApiServiceCollectionExtensionsTestsHelpers.InvokeAddBoundaryOpenApiDocuments(services, ["tours", "Tours"]);

        // Assert
        var exception = action.ShouldThrow<TargetInvocationException>();
        exception.InnerException.ShouldBeOfType<ArgumentException>();
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
        document.Paths.Keys.ShouldContain("/tours");
        document.Paths.Keys.ShouldContain("/tours/{id}");
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
        document.Paths.Keys.ShouldContain("/tours");
        document.Paths.Keys.ShouldNotContain("/tours-archive");
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
    public async Task Documents_bearer_authentication_only_for_protected_operations()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocumentFromApplication("tours", app =>
        {
            app.MapGroup("/tours")
                .WithGroupName("tours")
                .WithTags("tours")
                .MapGet("/", () => TypedResults.Ok())
                .RequireAuthorization();
            app.MapGroup("/tours")
                .WithGroupName("tours")
                .WithTags("tours")
                .MapGet("/public", () => TypedResults.Ok())
                .AllowAnonymous();
        });

        // Act
        var protectedPath = document.Paths["/tours"].ShouldNotBeNull();
        var protectedOperations = protectedPath.Operations.ShouldNotBeNull();
        var protectedOperation = protectedOperations[HttpMethod.Get].ShouldNotBeNull();
        var publicPath = document.Paths["/tours/public"].ShouldNotBeNull();
        var publicOperations = publicPath.Operations.ShouldNotBeNull();
        var publicOperation = publicOperations[HttpMethod.Get].ShouldNotBeNull();

        // Assert
        var components = document.Components.ShouldNotBeNull();
        var securitySchemes = components.SecuritySchemes.ShouldNotBeNull();
        securitySchemes.ContainsKey(OpenApiAuthenticationDefaults.BearerSecuritySchemeName).ShouldBeTrue();
        var security = protectedOperation.Security.ShouldNotBeNull();
        security.ShouldHaveSingleItem();
        var protectedResponses = protectedOperation.Responses.ShouldNotBeNull();
        protectedResponses.ContainsKey("401").ShouldBeTrue();
        protectedResponses.ContainsKey("403").ShouldBeTrue();
        publicOperation.Security.ShouldBeNull();
        var publicResponses = publicOperation.Responses.ShouldNotBeNull();
        publicResponses.ContainsKey("401").ShouldBeFalse();
        publicResponses.ContainsKey("403").ShouldBeFalse();
    }

    [Fact]
    public async Task Documents_bearer_authentication_for_protected_constrained_routes()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocumentFromApplication("tours", app =>
        {
            app.MapGroup("/tours")
                .WithGroupName("tours")
                .WithTags("tours")
                .MapGet("/{id:guid}", (Guid id) => TypedResults.Ok(id))
                .RequireAuthorization();
        });

        // Act
        var path = document.Paths["/tours/{id}"].ShouldNotBeNull();
        var operation = path.Operations.ShouldNotBeNull()[HttpMethod.Get].ShouldNotBeNull();

        // Assert
        operation.Security.ShouldNotBeNull().ShouldHaveSingleItem();
        operation.Responses.ShouldNotBeNull().ContainsKey("401").ShouldBeTrue();
        operation.Responses.ShouldNotBeNull().ContainsKey("403").ShouldBeTrue();
    }

    [Fact]
    public async Task Allows_anonymous_metadata_to_override_direct_authorization()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocumentFromApplication("tours", app =>
        {
            app.MapGroup("/tours")
                .WithGroupName("tours")
                .WithTags("tours")
                .MapGet("/public", () => TypedResults.Ok())
                .RequireAuthorization()
                .AllowAnonymous();
        });

        // Act
        var path = document.Paths["/tours/public"].ShouldNotBeNull();
        var operation = path.Operations.ShouldNotBeNull()[HttpMethod.Get].ShouldNotBeNull();
        var hasBearerScheme = document.Components?.SecuritySchemes?.ContainsKey(OpenApiAuthenticationDefaults.BearerSecuritySchemeName) ?? false;

        // Assert
        operation.Security.ShouldBeNull();
        operation.Responses.ShouldNotBeNull().ContainsKey("401").ShouldBeFalse();
        operation.Responses.ShouldNotBeNull().ContainsKey("403").ShouldBeFalse();
        hasBearerScheme.ShouldBeFalse();
    }

    [Fact]
    public async Task Omits_bearer_scheme_when_all_operations_are_anonymous()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocumentFromApplication("tours", app =>
        {
            app.MapGroup("/tours")
                .WithGroupName("tours")
                .WithTags("tours")
                .MapGet("/public", () => TypedResults.Ok())
                .AllowAnonymous();
        });

        // Act
        var hasBearerScheme = document.Components?.SecuritySchemes?.ContainsKey(OpenApiAuthenticationDefaults.BearerSecuritySchemeName) ?? false;

        // Assert
        hasBearerScheme.ShouldBeFalse();
    }

    [Fact]
    public async Task Omits_bearer_authentication_when_operation_has_no_authorization_metadata()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocumentFromApplication("tours", app =>
        {
            app.MapGroup("/tours")
                .WithGroupName("tours")
                .WithTags("tours")
                .MapGet("/public", () => TypedResults.Ok());
        });

        // Act
        var path = document.Paths["/tours/public"].ShouldNotBeNull();
        var operation = path.Operations.ShouldNotBeNull()[HttpMethod.Get].ShouldNotBeNull();
        var hasBearerScheme = document.Components?.SecuritySchemes?.ContainsKey(OpenApiAuthenticationDefaults.BearerSecuritySchemeName) ?? false;

        // Assert
        operation.Security.ShouldBeNull();
        operation.Responses.ShouldNotBeNull().ContainsKey("401").ShouldBeFalse();
        operation.Responses.ShouldNotBeNull().ContainsKey("403").ShouldBeFalse();
        hasBearerScheme.ShouldBeFalse();
    }

    [Fact]
    public async Task Documents_bearer_authentication_when_fallback_policy_requires_authenticated_users()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocumentFromApplication(
            "tours",
            services => services.AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()),
            app =>
            {
                app.MapGroup("/tours")
                    .WithGroupName("tours")
                    .WithTags("tours")
                    .MapGet("/managed", () => TypedResults.Ok());
            });

        // Act
        var path = document.Paths["/tours/managed"].ShouldNotBeNull();
        var operation = path.Operations.ShouldNotBeNull()[HttpMethod.Get].ShouldNotBeNull();

        // Assert
        operation.Security.ShouldNotBeNull().ShouldHaveSingleItem();
        operation.Responses.ShouldNotBeNull().ContainsKey("401").ShouldBeTrue();
        operation.Responses.ShouldNotBeNull().ContainsKey("403").ShouldBeTrue();
    }

    [Fact]
    public async Task Documents_bearer_authentication_when_fallback_policy_requires_a_role()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateDocumentFromApplication(
            "tours",
            services => services.AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireRole("Admin").Build()),
            app =>
            {
                app.MapGroup("/tours")
                    .WithGroupName("tours")
                    .WithTags("tours")
                    .MapGet("/public", () => TypedResults.Ok());
            });

        // Act
        var path = document.Paths["/tours/public"].ShouldNotBeNull();
        var operation = path.Operations.ShouldNotBeNull()[HttpMethod.Get].ShouldNotBeNull();

        // Assert
        operation.Security.ShouldNotBeNull().ShouldHaveSingleItem();
        operation.Responses.ShouldNotBeNull().ContainsKey("401").ShouldBeTrue();
        operation.Responses.ShouldNotBeNull().ContainsKey("403").ShouldBeTrue();
    }

    [Fact]
    public async Task Documents_bearer_authentication_when_operation_has_direct_authorization_policy_metadata()
    {
        // Arrange
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var document = await OpenApiDocumentFactory.CreateDocumentFromApplication("tours", app =>
        {
            app.MapGroup("/tours")
                .WithGroupName("tours")
                .WithTags("tours")
                .MapGet("/managed", () => TypedResults.Ok())
                .RequireAuthorization(policy);
        });

        // Act
        var path = document.Paths["/tours/managed"].ShouldNotBeNull();
        var operation = path.Operations.ShouldNotBeNull()[HttpMethod.Get].ShouldNotBeNull();

        // Assert
        operation.Security.ShouldNotBeNull().ShouldHaveSingleItem();
        operation.Responses.ShouldNotBeNull().ContainsKey("401").ShouldBeTrue();
        operation.Responses.ShouldNotBeNull().ContainsKey("403").ShouldBeTrue();
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
        // Arrange
        var services = OpenApiTestServiceCollectionFactory.Create();
        var version = new ApiVersionDefinition(new ApiVersion(1));

        // Act
        Action action = () => services.AddApiVersionOpenApiDocuments([version], routePrefix);

        // Assert
        var exception = action.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("routePrefix");
    }

}
