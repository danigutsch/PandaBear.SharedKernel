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
    public async Task Normalizes_Malformed_Multipart_Form_AllOf_Entries()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateUploadsDocument(group =>
            group.MapPost("/commit", ([AsParameters] TestCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery());

        // Act
        var schema = GetMultipartSchema(document, "/uploads/commit");

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
    public async Task Leaves_Root_Required_Null_When_No_Form_Fields_Are_Required()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateUploadsDocument(group =>
            group.MapPost("/optional", ([AsParameters] TestOptionalCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery());

        // Act
        var schema = GetMultipartSchema(document, "/uploads/optional");

        // Assert
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.NotNull(schema.AllOf);
        Assert.Null(schema.Required);
    }

    [Fact]
    public async Task Preserves_Valid_Multipart_Form_AllOf_Entries()
    {
        // Arrange
        var document = await OpenApiDocumentFactory.CreateUploadsDocument(group =>
            group.MapPost("/files", (IFormFile firstFile, IFormFile secondFile) => TypedResults.Ok())
                .DisableAntiforgery());

        // Act
        var schema = GetMultipartSchema(document, "/uploads/files");

        // Assert
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.NotNull(schema.AllOf);
        Assert.Collection(
            schema.AllOf,
            item => Assert.Contains("firstFile", item.Properties!.Keys),
            item => Assert.Contains("secondFile", item.Properties!.Keys));
    }

    private static OpenApiSchema GetMultipartSchema(OpenApiDocument document, string path)
    {
        Assert.True(document.Paths.TryGetValue(path, out var pathItem), $"Expected OpenAPI path '{path}' to exist.");
        Assert.NotNull(pathItem);
        Assert.NotNull(pathItem.Operations);
        Assert.True(pathItem.Operations.TryGetValue(HttpMethod.Post, out var operation), $"Expected POST operation for '{path}'.");
        Assert.NotNull(operation);

        var schema = operation.RequestBody?.Content?["multipart/form-data"].Schema;

        Assert.NotNull(schema);
        Assert.IsType<OpenApiSchema>(schema);

        return (OpenApiSchema)schema;
    }
}
