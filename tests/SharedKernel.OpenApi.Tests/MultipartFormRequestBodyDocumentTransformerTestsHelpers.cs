using Microsoft.OpenApi;
using System.Reflection;

namespace SharedKernel.OpenApi.Tests;

internal static class MultipartFormRequestBodyDocumentTransformerTestsHelpers
{
    public static object InvokePrivateStaticMethod(string methodName, object?[] arguments)
    {
        var method = typeof(MultipartFormRequestBodyDocumentTransformer).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not locate private method '{methodName}'.");

        return method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"Private method '{methodName}' returned null.");
    }

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

    public static void InvokePrivateStaticVoidMethod(string methodName, object?[] arguments)
    {
        var method = typeof(MultipartFormRequestBodyDocumentTransformer).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not locate private method '{methodName}'.");

        _ = method.Invoke(null, arguments);
    }

    public static OpenApiSchema GetMultipartSchema(OpenApiDocument document, string path)
    {
        (document.Paths.TryGetValue(path, out var pathItem)).ShouldBeTrue($"Expected OpenAPI path '{path}' to exist.");
        _ = (pathItem).ShouldNotBeNull();
        (pathItem.Operations).ShouldNotBeNull();
        (pathItem.Operations.TryGetValue(HttpMethod.Post, out var operation)).ShouldBeTrue($"Expected POST operation for '{path}'.");
        _ = (operation).ShouldNotBeNull();

        var schema = operation.RequestBody?.Content?["multipart/form-data"].Schema;

        _ = (schema).ShouldNotBeNull();
        return (schema).ShouldBeOfType<OpenApiSchema>();
    }
}
