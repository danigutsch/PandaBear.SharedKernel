using Microsoft.AspNetCore.Builder;
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
    /// Builds an app, maps an uploads route group, and returns the generated OpenAPI document.
    /// </summary>
    /// <param name="configureGroup">Configures endpoints inside the uploads route group.</param>
    /// <returns>The generated OpenAPI document.</returns>
    public static async Task<OpenApiDocument> CreateUploadsDocument(Action<RouteGroupBuilder> configureGroup)
    {
        ArgumentNullException.ThrowIfNull(configureGroup);

        const string documentName = "uploads";
        var builder = WebApplication.CreateBuilder();
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
}
