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
    public async Task Normalizes_malformed_multipart_form_allof_entries()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateUploadsDocument(group =>
            group.MapPost("/commit", ([AsParameters] TestCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery());

        // Act
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(document, "/uploads/commit");

        // Assert
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.NotNull(schema.Properties);
        Assert.NotNull(schema.AllOf);
        Assert.DoesNotContain(schema.AllOf, static item => item.Type != JsonSchemaType.Object || item.Properties is null);

        var propertyNames = schema.AllOf
            .Where(static item => item.Properties is not null)
            .SelectMany(static item => item.Properties!.Keys)
            .ToArray();

        var requiredContainer = schema.AllOf
            .FirstOrDefault(static item => item.Properties?.ContainsKey("file") == true);

        Assert.NotNull(requiredContainer);
        Assert.NotNull(requiredContainer.Required);
        Assert.Contains("file", requiredContainer.Required);
        Assert.Contains("file", propertyNames);
        Assert.Contains("conflictResolutions", propertyNames);
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
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.NotNull(schema.AllOf);
        Assert.Null(schema.Required);
    }

    [Fact]
    public async Task Normalizes_already_malformed_multipart_schemas_using_runtime_context()
    {
        // Arrange
        var normalizedDocument = await OpenApiDocumentFactory.ExecuteWithCapturedContext(
            "uploads",
            group => group.MapPost("/commit", ([AsParameters] TestCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery(),
            async (_, context) =>
            {
                var document = MultipartFormRequestBodyDocumentTransformerTestsHelpers.CreateMalformedMultipartDocument("/uploads/commit");
                var transformer = new MultipartFormRequestBodyDocumentTransformer();

                await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);
                return document;
            });

        // Assert
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(normalizedDocument, "/uploads/commit");
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.NotNull(schema.AllOf);
        Assert.Equal(2, schema.AllOf.Count);
        Assert.All(schema.AllOf, item => Assert.Equal(JsonSchemaType.Object, item.Type));
        Assert.Contains(schema.AllOf, static item => item.Properties?.ContainsKey("file") == true);
        Assert.Contains(schema.AllOf, static item => item.Properties?.ContainsKey("conflictResolutions") == true);
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
        Assert.NotNull(schema.AllOf);
        Assert.Single(schema.AllOf);
        Assert.Null(schema.Properties);
    }

    [Fact]
    public async Task Skips_invalid_form_parameters_during_malformed_normalization()
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
                    "NormalizeMalformedMultipartSchema",
                    [schema, new[] { invalidParameter }, context, TestContext.Current.CancellationToken]);

                return schema;
            });

        // Assert
        Assert.NotNull(normalizedSchema.AllOf);
        Assert.Empty(normalizedSchema.AllOf);
        Assert.Null(normalizedSchema.Required);
    }

    [Fact]
    public async Task Clears_root_required_when_malformed_normalization_uses_only_optional_form_fields()
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
        Assert.NotNull(schema.AllOf);
        Assert.Null(schema.Required);
    }

    [Fact]
    public async Task Preserves_valid_multipart_form_allof_entries()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateUploadsDocument(group =>
            group.MapPost("/files", (IFormFile firstFile, IFormFile secondFile) => TypedResults.Ok())
                .DisableAntiforgery());

        // Act
        var schema = MultipartFormRequestBodyDocumentTransformerTestsHelpers.GetMultipartSchema(document, "/uploads/files");

        // Assert
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.NotNull(schema.AllOf);
        Assert.Collection(
            schema.AllOf,
            item => Assert.Contains("firstFile", item.Properties!.Keys),
            item => Assert.Contains("secondFile", item.Properties!.Keys));
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
        Assert.True(document.Paths.ContainsKey("/uploads/commit"));
    }

    [Fact]
    public void Returns_false_when_multipart_normalization_has_no_allof_entries()
    {
        var schema = new OpenApiSchema();

        var result = Assert.IsType<bool>(MultipartFormRequestBodyDocumentTransformerTestsHelpers.InvokePrivateStaticMethod(
            "RequiresMultipartSchemaNormalization",
            [schema]));

        Assert.False(result);
    }

    [Fact]
    public void Returns_without_changing_requiredness_when_allof_is_missing()
    {
        var schema = new OpenApiSchema();
        MultipartFormRequestBodyDocumentTransformerTestsHelpers.InvokePrivateStaticVoidMethod(
            "PreserveRequirednessOnMultipartAllOfEntries",
            [schema, Array.Empty<Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription>()]);

        Assert.Null(schema.Required);
    }

    [Fact]
    public void Skips_requiredness_when_no_container_matches_the_required_parameter()
    {
        var schema = new OpenApiSchema
        {
            AllOf =
            [
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["other"] = new OpenApiSchema()
                    }
                }
            ]
        };

        var parameter = new Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription
        {
            Name = "file",
            IsRequired = true
        };
        MultipartFormRequestBodyDocumentTransformerTestsHelpers.InvokePrivateStaticVoidMethod(
            "PreserveRequirednessOnMultipartAllOfEntries",
            [schema, new[] { parameter }]);

        Assert.Null(schema.AllOf[0].Required);
    }

}
