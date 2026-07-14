using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.AspNetCore.Tests;

internal sealed class OpenApiBuildGenerationDataProtectionTestHost : IDisposable
{
    private readonly ServiceProvider serviceProvider;

    private OpenApiBuildGenerationDataProtectionTestHost(ServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        DataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
    }

    public IDataProtectionProvider DataProtectionProvider { get; }

    public static OpenApiBuildGenerationDataProtectionTestHost Create()
    {
        var services = new ServiceCollection();
        OpenApiBuildGenerationServiceCollectionExtensions.AddEphemeralDataProtection(services);

        return new OpenApiBuildGenerationDataProtectionTestHost(services.BuildServiceProvider());
    }

    public void Dispose()
    {
        serviceProvider.Dispose();
    }
}
