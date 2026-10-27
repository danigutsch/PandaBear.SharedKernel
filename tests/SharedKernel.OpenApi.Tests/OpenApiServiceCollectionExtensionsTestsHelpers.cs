using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.OpenApi.Tests;

/// <summary>
/// Reflection helpers for calling OpenAPI service collection extensions with arguments the compiler would reject.
/// </summary>
internal static class OpenApiServiceCollectionExtensionsTestsHelpers
{
    /// <summary>
    /// Invokes the AddBoundaryOpenApiDocuments overload taking an IReadOnlyCollection of names by reflection so null arguments can be passed.
    /// </summary>
    /// <param name="services">The service collection, which may be null.</param>
    /// <param name="boundaryNames">The boundary names, which may be null.</param>
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
