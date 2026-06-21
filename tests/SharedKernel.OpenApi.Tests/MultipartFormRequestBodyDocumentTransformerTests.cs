using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using System.Reflection;
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
    public async Task Normalizes_Already_Malformed_Multipart_Schemas_Using_Runtime_Context()
    {
        // Arrange
        var normalizedDocument = await OpenApiDocumentFactory.ExecuteWithCapturedContext(
            "uploads",
            group => group.MapPost("/commit", ([AsParameters] TestCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery(),
            async (_, context) =>
            {
                var document = CreateMalformedMultipartDocument("/uploads/commit");
                var transformer = new MultipartFormRequestBodyDocumentTransformer();

                await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);
                return document;
            });

        // Assert
        var schema = GetMultipartSchema(normalizedDocument, "/uploads/commit");
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.NotNull(schema.AllOf);
        Assert.Equal(2, schema.AllOf.Count);
        Assert.All(schema.AllOf, item => Assert.Equal(JsonSchemaType.Object, item.Type));
        Assert.Contains(schema.AllOf, static item => item.Properties?.ContainsKey("file") == true);
        Assert.Contains(schema.AllOf, static item => item.Properties?.ContainsKey("conflictResolutions") == true);
    }

    [Fact]
    public async Task Ignores_Malformed_Multipart_Schemas_When_No_Form_Parameters_Exist()
    {
        // Arrange
        var untouchedDocument = await OpenApiDocumentFactory.ExecuteWithCapturedContext(
            "uploads",
            group => group.MapPost("/commit", (TestJsonRequest body) => TypedResults.Ok(body))
                .DisableAntiforgery(),
            async (_, context) =>
            {
                var document = CreateMalformedMultipartDocument("/uploads/commit");
                var transformer = new MultipartFormRequestBodyDocumentTransformer();

                await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);
                return document;
            });

        // Assert
        var schema = GetMultipartSchema(untouchedDocument, "/uploads/commit");
        Assert.NotNull(schema.AllOf);
        Assert.Single(schema.AllOf);
        Assert.Null(schema.Properties);
    }

    [Fact]
    public async Task Skips_Invalid_Form_Parameters_During_Malformed_Normalization()
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

                await InvokePrivateStaticTaskMethod(
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
    public async Task Clears_Root_Required_When_Malformed_Normalization_Uses_Only_Optional_Form_Fields()
    {
        // Arrange
        var normalizedDocument = await OpenApiDocumentFactory.ExecuteWithCapturedContext(
            "uploads",
            group => group.MapPost("/optional", ([AsParameters] TestOptionalCommitImportFormDto form) => TypedResults.Ok())
                .DisableAntiforgery(),
            async (_, context) =>
            {
                var document = CreateMalformedMultipartDocument("/uploads/optional");
                var transformer = new MultipartFormRequestBodyDocumentTransformer();

                await transformer.TransformAsync(document, context, TestContext.Current.CancellationToken);
                return document;
            });

        // Assert
        var schema = GetMultipartSchema(normalizedDocument, "/uploads/optional");
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

    [Fact]
    public async Task Ignores_Paths_Without_Operations()
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
    public void Returns_False_When_Multipart_Normalization_Has_No_AllOf_Entries()
    {
        var schema = new OpenApiSchema();

        var result = Assert.IsType<bool>(InvokePrivateStaticMethod(
            "RequiresMultipartSchemaNormalization",
            [schema]));

        Assert.False(result);
    }

    [Fact]
    public void Returns_Without_Changing_Requiredness_When_AllOf_Is_Missing()
    {
        var schema = new OpenApiSchema();

        InvokePrivateStaticVoidMethod(
            "PreserveRequirednessOnMultipartAllOfEntries",
            [schema, Array.Empty<Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription>()]);

        Assert.Null(schema.Required);
    }

    [Fact]
    public void Skips_Requiredness_When_No_Container_Matches_The_Required_Parameter()
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

        InvokePrivateStaticVoidMethod(
            "PreserveRequirednessOnMultipartAllOfEntries",
            [schema, new[] { parameter }]);

        Assert.Null(schema.AllOf[0].Required);
    }

    private static OpenApiDocument CreateMalformedMultipartDocument(string path)
    {
        return new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                [path] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = new OpenApiOperation
                        {
                            RequestBody = new OpenApiRequestBody
                            {
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["multipart/form-data"] = new()
                                    {
                                        Schema = new OpenApiSchema
                                        {
                                            AllOf = [new OpenApiSchema()],
                                            Properties = null
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static object InvokePrivateStaticMethod(string methodName, object?[] arguments)
    {
        var method = typeof(MultipartFormRequestBodyDocumentTransformer).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not locate private method '{methodName}'.");

        return method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"Private method '{methodName}' returned null.");
    }

    private static void InvokePrivateStaticVoidMethod(string methodName, object?[] arguments)
    {
        var method = typeof(MultipartFormRequestBodyDocumentTransformer).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not locate private method '{methodName}'.");

        _ = method.Invoke(null, arguments);
    }

    private static async Task InvokePrivateStaticTaskMethod(string methodName, object?[] arguments)
    {
        var method = typeof(MultipartFormRequestBodyDocumentTransformer).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not locate private method '{methodName}'.");

        var task = method.Invoke(null, arguments) as Task
            ?? throw new InvalidOperationException($"Private method '{methodName}' did not return a Task.");

        await task;
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
        return Assert.IsType<OpenApiSchema>(schema);
    }
}
