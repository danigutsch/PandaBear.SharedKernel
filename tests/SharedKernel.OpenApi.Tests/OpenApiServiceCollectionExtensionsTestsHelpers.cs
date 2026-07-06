using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.OpenApi.Tests;

internal static class OpenApiServiceCollectionExtensionsTestsHelpers
{
    public static void InvokeAddBoundaryOpenApiDocuments(IServiceCollection? services, IReadOnlyCollection<string>? boundaryNames)
    {
        var method = typeof(OpenApiServiceCollectionExtensions)
            .GetMethods()
            .SingleOrDefault(static candidate =>
                candidate.Name == nameof(OpenApiServiceCollectionExtensions.AddBoundaryOpenApiDocuments)
                && candidate.GetParameters()[1].ParameterType == typeof(IReadOnlyCollection<string>))
            ?? throw new InvalidOperationException("Could not locate AddBoundaryOpenApiDocuments.");

        _ = method.Invoke(null, [services, boundaryNames]);
    }
}
