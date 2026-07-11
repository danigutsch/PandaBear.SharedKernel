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
        (schema.Type).ShouldBe(JsonSchemaType.Object);
        (schema.Properties).ShouldNotBeNull();
        (schema.AllOf).ShouldNotBeNull();
        (schema.AllOf).ShouldNotContain(static item => item.Type != JsonSchemaType.Object || item.Properties is null);

        var propertyNames = schema.AllOf
            .Where(static item => item.Properties is not null)
            .SelectMany(static item => item.Properties!.Keys)
            .ToArray();

        var requiredContainer = schema.AllOf
            .FirstOrDefault(static item => item.Properties?.ContainsKey("file") == true);

        _ = (requiredContainer).ShouldNotBeNull();
        (requiredContainer.Required).ShouldNotBeNull();
        (requiredContainer.Required).ShouldContain("file");
        (propertyNames).ShouldContain("file");
        (propertyNames).ShouldContain("conflictResolutions");
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
        (schema.AllOf).ShouldNotBeNull();
        (schema.Required).ShouldBeNull();
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
        (schema.Type).ShouldBe(JsonSchemaType.Object);
        (schema.AllOf).ShouldNotBeNull();
        (schema.AllOf.Count).ShouldBe(2);
        (schema.AllOf).ShouldAllSatisfy(item => (item.Type).ShouldBe(JsonSchemaType.Object));
        (schema.AllOf).ShouldContain(static item => item.Properties?.ContainsKey("file") == true);
        (schema.AllOf).ShouldContain(static item => item.Properties?.ContainsKey("conflictResolutions") == true);
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
        (normalizedSchema.AllOf).ShouldNotBeNull();
        (normalizedSchema.AllOf).ShouldBeEmpty();
        (normalizedSchema.Required).ShouldBeNull();
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
        (schema.AllOf).ShouldNotBeNull();
        (schema.Required).ShouldBeNull();
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
        (schema.Type).ShouldBe(JsonSchemaType.Object);
        (schema.AllOf).ShouldNotBeNull();
        (schema.AllOf).ShouldMatchCollection(item => (item.Properties!.Keys).ShouldContain("firstFile"), item => (item.Properties!.Keys).ShouldContain("secondFile"));
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

    [Fact]
    public void Returns_false_when_multipart_normalization_has_no_allof_entries()
    {
        var schema = new OpenApiSchema();

        var result = (MultipartFormRequestBodyDocumentTransformerTestsHelpers.InvokePrivateStaticMethod(
            "RequiresMultipartSchemaNormalization",
            [schema])).ShouldBeOfType<bool>();

        (result).ShouldBeFalse();
    }

    [Fact]
    public void Returns_without_changing_requiredness_when_allof_is_missing()
    {
        var schema = new OpenApiSchema();
        MultipartFormRequestBodyDocumentTransformerTestsHelpers.InvokePrivateStaticVoidMethod(
            "PreserveRequirednessOnMultipartAllOfEntries",
            [schema, Array.Empty<Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription>()]);

        (schema.Required).ShouldBeNull();
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

        (schema.AllOf[0].Required).ShouldBeNull();
    }

}
