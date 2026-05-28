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
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentNullException.ThrowIfNull(configureGroup);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddBoundaryOpenApiDocuments([documentName]);

        await using var app = builder.Build();
        var group = app.MapGroup($"/{documentName}")
            .WithGroupName(documentName)
            .WithTags(documentName);

        configureGroup(group);

        await app.StartAsync();

        using var scope = app.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredKeyedService<IOpenApiDocumentProvider>(documentName);
        return await provider.GetOpenApiDocumentAsync();
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
