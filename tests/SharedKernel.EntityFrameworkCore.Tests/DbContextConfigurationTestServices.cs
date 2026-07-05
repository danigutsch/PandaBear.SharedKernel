using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal static class DbContextConfigurationTestServices
{
    public static IServiceCollection Create()
    {
        return new ServiceCollection();
    }
}
