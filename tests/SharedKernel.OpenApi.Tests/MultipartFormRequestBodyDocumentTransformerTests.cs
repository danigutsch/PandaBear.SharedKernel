using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Xunit;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Verifies generic multipart form OpenAPI normalization behavior.
/// </summary>
public sealed class MultipartFormRequestBodyDocumentTransformerTests
{
    [Fact]
    public async Task Normalizes_multipart_form_schemas()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateUploadsDocument(group =>
            group.MapPost("/commit", ([AsParameters] TestCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery());

        // Act
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(document, "/uploads/commit");

        // Assert
        (schema.Type).ShouldBe(JsonSchemaType.Object);
        (schema.Properties).ShouldNotBeNull();
        (schema.AllOf).ShouldBeNull();
        (schema.Required).ShouldNotBeNull();
        (schema.Required).ShouldContain("file");
        (schema.Properties.Keys).ShouldContain("file");
        (schema.Properties.Keys).ShouldContain("conflictResolutions");
    }

    [Fact]
    public async Task Normalizes_multipart_form_schemas_with_constrained_route_parameters()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateUploadsDocument(group =>
            group.MapPost("/commit/{id:guid}", (Guid id, [AsParameters] TestCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery());

        // Act
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(document, "/uploads/commit/{id}");

        // Assert
        (schema.Properties).ShouldNotBeNull();
        (schema.AllOf).ShouldBeNull();
        (schema.Properties.Keys).ShouldContain("file");
        (schema.Properties.Keys).ShouldContain("conflictResolutions");
    }

    [Fact]
    public async Task Leaves_root_required_null_when_no_form_fields_are_required()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateUploadsDocument(group =>
            group.MapPost("/optional", ([AsParameters] TestOptionalCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery());

        // Act
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(document, "/uploads/optional");

        // Assert
        (schema.Type).ShouldBe(JsonSchemaType.Object);
        (schema.Properties).ShouldNotBeNull();
        (schema.AllOf).ShouldBeNull();
        (schema.Required).ShouldBeNull();
    }

    [Fact]
    public async Task Normalizes_multipart_schemas_using_runtime_context()
    {
        // Arrange
        var normalizedDocument = await OpenApiDocumentFactory.ExecuteWithCapturedContext(
            "uploads",
            group => group.MapPost("/commit", ([AsParameters] TestCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery(),
            async (_, context) =>
            {
                var document = MultipartFormRequestBodyDocumentTransformerTestsHelpers.CreateMalformedMultipartDocument("/uploads/commit");
                var multipartSchema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(document, "/uploads/commit");
                multipartSchema.AllOf =
                [
                    new OpenApiSchema(),
                    new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["conflictResolutions"] = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Format = "preserved-format"
                            }
                        },
                        Required = new HashSet<string>(StringComparer.Ordinal)
                        {
                            "conflictResolutions"
                        }
                    }
                ];
                var transformer = new MultipartFormRequestBodyDocumentTransformer();

                await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);
                return document;
            });

        // Assert
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(normalizedDocument, "/uploads/commit");
        (schema.Type).ShouldBe(JsonSchemaType.Object);
        (schema.Properties).ShouldNotBeNull();
        (schema.AllOf).ShouldBeNull();
        (schema.Properties.Keys).ShouldContain("file");
        (schema.Properties.Keys).ShouldContain("conflictResolutions");
        (schema.Required).ShouldNotBeNull().ShouldContain("conflictResolutions");
        var conflictResolutionSchema = schema.Properties["conflictResolutions"].ShouldBeOfType<OpenApiSchema>();
        conflictResolutionSchema.Format.ShouldBe("preserved-format");
    }

    [Fact]
    public async Task Ignores_malformed_multipart_schemas_when_no_form_parameters_exist()
    {
        // Arrange
        var untouchedDocument = await OpenApiDocumentFactory.ExecuteWithCapturedContext(
            "uploads",
            group => group.MapPost("/commit", (TestJsonRequest body) => TypedResults.Ok(body))
                .DisableAntiforgery(),
            async (_, context) =>
            {
                var document = MultipartFormRequestBodyDocumentTransformerTestsHelpers.CreateMalformedMultipartDocument("/uploads/commit");
                var transformer = new MultipartFormRequestBodyDocumentTransformer();

                await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);
                return document;
            });

        // Assert
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(untouchedDocument, "/uploads/commit");
        (schema.AllOf).ShouldNotBeNull();
        (schema.AllOf).ShouldHaveSingleItem();
        (schema.Properties).ShouldBeNull();
    }

    [Fact]
    public async Task Skips_invalid_form_parameters_during_multipart_normalization()
    {
        // Arrange
        var normalizedSchema = await OpenApiDocumentFactory.ExecuteWithCapturedContext(
            "uploads",
            group => group.MapPost("/commit", ([AsParameters] TestCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery(),
            async (_, context) =>
            {
                var schema = new OpenApiSchema();
                var invalidParameter = new Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription
                {
                    Name = string.Empty
                };

                await MultipartFormRequestBodyDocumentTransformerTestsHelpers.InvokePrivateStaticTaskMethod(
                    "NormalizeMultipartSchema",
                    [schema, new[] { invalidParameter }, context, TestContext.Current.CancellationToken]);

                return schema;
            });

        // Assert
        (normalizedSchema.AllOf).ShouldBeNull();
        (normalizedSchema.Required).ShouldBeNull();
    }

    [Fact]
    public async Task Clears_root_required_when_multipart_normalization_uses_only_optional_form_fields()
    {
        // Arrange
        var normalizedDocument = await OpenApiDocumentFactory.ExecuteWithCapturedContext(
            "uploads",
            group => group.MapPost("/optional", ([AsParameters] TestOptionalCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery(),
            async (_, context) =>
            {
                var document = MultipartFormRequestBodyDocumentTransformerTestsHelpers.CreateMalformedMultipartDocument("/uploads/optional");
                var transformer = new MultipartFormRequestBodyDocumentTransformer();

                await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);
                return document;
            });

        // Assert
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(normalizedDocument, "/uploads/optional");
        (schema.AllOf).ShouldBeNull();
        (schema.Required).ShouldBeNull();
    }

    [Fact]
    public async Task Canonicalizes_valid_multipart_form_allof_entries()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateUploadsDocument(group =>
            group.MapPost("/files", (IFormFile firstFile, IFormFile secondFile) => TypedResults.Ok())
                .DisableAntiforgery());

        // Act
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(document, "/uploads/files");

        // Assert
        (schema.Type).ShouldBe(JsonSchemaType.Object);
        (schema.Properties).ShouldNotBeNull();
        (schema.AllOf).ShouldBeNull();
        (schema.Properties.Keys).ShouldContain("firstFile");
        (schema.Properties.Keys).ShouldContain("secondFile");
    }

    [Fact]
    public async Task Ignores_paths_without_operations()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.ExecuteWithCapturedContext(
            "uploads",
            group => group.MapPost("/commit", ([AsParameters] TestCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery(),
            async (_, context) =>
            {
                var malformedDocument = new OpenApiDocument
                {
                    Paths = new OpenApiPaths
                    {
                        ["/uploads/commit"] = new OpenApiPathItem { Operations = null }
                    }
                };

                var transformer = new MultipartFormRequestBodyDocumentTransformer();
                await transformer.TransformAsync(malformedDocument, context, TestContext.Current.CancellationToken);
                return malformedDocument;
            });

        // Assert
        (document.Paths.ContainsKey("/uploads/commit")).ShouldBeTrue();
    }
}
