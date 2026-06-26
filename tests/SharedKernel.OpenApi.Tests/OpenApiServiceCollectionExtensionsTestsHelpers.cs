using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.OpenApi.Tests;

internal static class OpenApiServiceCollectionExtensionsTestsHelpers
{
    public static void InvokeAddBoundaryOpenApiDocuments(IServiceCollection? services, IReadOnlyCollection<string>? boundaryNames)
    {
        var method = typeof(OpenApiServiceCollectionExtensions).GetMethod(nameof(OpenApiServiceCollectionExtensions.AddBoundaryOpenApiDocuments))
            ?? throw new InvalidOperationException("Could not locate AddBoundaryOpenApiDocuments.");

        _ = method.Invoke(null, [services, boundaryNames]);
    }
}
