using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.OpenApi.Tests;

internal static class OpenApiTestServiceCollectionFactory
{
    public static IServiceCollection Create()
    {
        return new ServiceCollection();
    }
}
