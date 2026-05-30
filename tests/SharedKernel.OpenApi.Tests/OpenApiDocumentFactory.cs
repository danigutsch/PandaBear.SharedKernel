using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Creates OpenAPI documents from a lightweight in-process ASP.NET Core host.
/// </summary>
internal static class OpenApiDocumentFactory
{
    /// <summary>
    /// Builds an app and returns the generated OpenAPI document for a named boundary.
    /// </summary>
    /// <param name="documentName">The OpenAPI document name to register and retrieve.</param>
    /// <param name="configureGroup">Configures endpoints inside the named route group.</param>
    /// <returns>The generated OpenAPI document.</returns>
    public static async Task<OpenApiDocument> CreateDocument(string documentName, Action<RouteGroupBuilder> configureGroup)
    {
        return await ExecuteWithCapturedContext(
            documentName,
            configureGroup,
            static (document, _, _) => Task.FromResult(document));
    }

    /// <summary>
    /// Builds an app and executes a callback while the runtime transformer context is still alive.
    /// </summary>
    /// <param name="documentName">The OpenAPI document name to register and retrieve.</param>
    /// <param name="configureGroup">Configures endpoints inside the named route group.</param>
    /// <param name="execute">Runs while the app and scoped services are still alive.</param>
    /// <typeparam name="TResult">The result type returned by the callback.</typeparam>
    /// <returns>The callback result.</returns>
    public static async Task<TResult> ExecuteWithCapturedContext<TResult>(
        string documentName,
        Action<RouteGroupBuilder> configureGroup,
        Func<OpenApiDocument, OpenApiDocumentTransformerContext, IServiceProvider, Task<TResult>> execute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentNullException.ThrowIfNull(configureGroup);
        ArgumentNullException.ThrowIfNull(execute);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddBoundaryOpenApiDocuments([documentName]);
        builder.Services.AddSingleton<OpenApiContextCapture>();
        builder.Services.AddTransient<CapturingOpenApiDocumentTransformer>();
        builder.Services.AddOpenApi(documentName, options => options.AddDocumentTransformer<CapturingOpenApiDocumentTransformer>());

        await using var app = builder.Build();
        var group = app.MapGroup($"/{documentName}")
            .WithGroupName(documentName)
            .WithTags(documentName);

        configureGroup(group);

        await app.StartAsync();

        using var scope = app.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredKeyedService<IOpenApiDocumentProvider>(documentName);
        var document = await provider.GetOpenApiDocumentAsync();
        var capture = scope.ServiceProvider.GetRequiredService<OpenApiContextCapture>();
        var context = capture.Context ?? throw new InvalidOperationException("OpenAPI transformer context was not captured.");

        return await execute(document, context, scope.ServiceProvider);
    }

    /// <summary>
    /// Builds an app, maps an uploads route group, and returns the generated OpenAPI document.
    /// </summary>
    /// <param name="configureGroup">Configures endpoints inside the uploads route group.</param>
    /// <returns>The generated OpenAPI document.</returns>
    public static async Task<OpenApiDocument> CreateUploadsDocument(Action<RouteGroupBuilder> configureGroup)
    {
        return await CreateDocument("uploads", configureGroup);
    }
}
