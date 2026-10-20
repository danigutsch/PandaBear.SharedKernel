using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore.Tests;

internal static class DbContextConfigurationTestServices
{
    public static IServiceCollection Create()
    {
        return new ServiceCollection();
    }
}
