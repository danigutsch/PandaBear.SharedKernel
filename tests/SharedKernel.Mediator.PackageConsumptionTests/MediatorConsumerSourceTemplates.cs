namespace SharedKernel.Mediator.PackageConsumptionTests;

internal static class MediatorConsumerSourceTemplates
{
    public static string CreateHarness(string @namespace)
    {
        return $$"""
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.Mediator;

            namespace {{@namespace}};

            public sealed class MediatorHarness : IDisposable
            {
                private readonly ServiceProvider provider;
                private readonly IServiceScope scope;

                private MediatorHarness(ServiceProvider provider, IServiceScope scope)
                {
                    this.provider = provider;
                    this.scope = scope;
                    Mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                }

                public IMediator Mediator { get; }

                public static MediatorHarness Create()
                {
                    var services = new ServiceCollection();
                    services.AddSharedKernelMediator();

                    var provider = services.BuildServiceProvider();
                    var scope = provider.CreateScope();
                    return new MediatorHarness(provider, scope);
                }

                public void Dispose()
                {
                    scope.Dispose();
                    provider.Dispose();
                }
            }
            """;
    }
}
