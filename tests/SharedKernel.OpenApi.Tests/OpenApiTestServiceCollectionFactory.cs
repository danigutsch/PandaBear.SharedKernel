using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.OpenApi.Tests;

internal static class OpenApiTestServiceCollectionFactory
{
    public static IServiceCollection Create()
    {
        return new ServiceCollection();
    }
}
