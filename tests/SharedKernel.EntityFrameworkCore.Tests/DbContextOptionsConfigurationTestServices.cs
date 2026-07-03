using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal static class DbContextOptionsConfigurationTestServices
{
    public static IServiceCollection Create() => new ServiceCollection();
}
