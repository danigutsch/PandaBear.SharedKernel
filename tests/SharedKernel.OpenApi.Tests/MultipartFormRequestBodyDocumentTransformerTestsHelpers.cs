using Microsoft.OpenApi;
using System.Reflection;

namespace PandaBear.OpenApi.Tests;

/// <summary>Reflection and document-building helpers for the multipart request body transformer tests.</summary>
internal static class MultipartFormRequestBodyDocumentTransformerTestsHelpers
{
    /// <summary>
    /// Calls a private static Task-returning method on the transformer by name and awaits it, failing if it is missing or not Task-returning.
    /// </summary>
    /// <param name="methodName">The name of the non-public static method.</param>
    /// <param name="arguments">The arguments passed to the method.</param>
    public static async Task InvokePrivateStaticTaskMethod(string methodName, object?[] arguments)
    {
        var method = typeof(MultipartFormRequestBodyDocumentTransformer).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not locate private method '{methodName}'.");

        var task = method.Invoke(null, arguments) as Task
            ?? throw new InvalidOperationException($"Private method '{methodName}' did not return a Task.");

        await task;
    }

    /// <summary>
    /// Builds an OpenAPI document whose POST multipart/form-data schema is malformed, with one empty allOf entry and null properties.
    /// </summary>
    /// <param name="path">The path under which the operation is registered.</param>
    public static OpenApiDocument CreateMalformedMultipartDocument(string path)
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

    /// <summary>
    /// Returns the multipart/form-data request body schema of the POST operation at the path, asserting that each part exists.
    /// </summary>
    /// <param name="document">The OpenAPI document to inspect.</param>
    /// <param name="path">The path of the operation.</param>
    public static OpenApiSchema GetMultipartSchema(OpenApiDocument document, string path)
    {
        document.Paths.TryGetValue(path, out var pathItem).ShouldBeTrue($"Expected OpenAPI path '{path}' to exist.");
        _ = pathItem.ShouldNotBeNull();
        pathItem.Operations.ShouldNotBeNull();
        pathItem.Operations.TryGetValue(HttpMethod.Post, out var operation).ShouldBeTrue($"Expected POST operation for '{path}'.");
        _ = operation.ShouldNotBeNull();

        var schema = operation.RequestBody?.Content?["multipart/form-data"].Schema;

        _ = schema.ShouldNotBeNull();
        return schema.ShouldBeOfType<OpenApiSchema>();
    }
}
