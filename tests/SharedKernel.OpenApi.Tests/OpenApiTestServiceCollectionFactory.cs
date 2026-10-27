using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.OpenApi.Tests;

/// <summary>Creates service collections for OpenAPI tests.</summary>
internal static class OpenApiTestServiceCollectionFactory
{
    /// <summary>Creates a new empty service collection.</summary>
    public static IServiceCollection Create()
    {
        return new ServiceCollection();
    }
}
