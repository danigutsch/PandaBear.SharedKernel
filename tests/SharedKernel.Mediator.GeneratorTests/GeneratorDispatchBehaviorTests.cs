using SharedKernel.Mediator.Testing.ReferenceDispatcher;

namespace SharedKernel.Mediator.GeneratorTests;

[Trait(TestTraits.CapabilityName, TestTraits.DispatchCapability)]
public sealed class GeneratorDispatchBehaviorTests
{
    [Fact]
    public async Task Generated_Request_Dispatch_Matches_Reference_Dispatcher()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed class LookupTour : IRequest<string>
            {
                public LookupTour(string code) => Code = code;

                public string Code { get; }
            }

            public sealed class LookupTourHandler : IRequestHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code.ToUpperInvariant());
                }
            }
            """;
        const string serviceProviderExtensionsSource = """
            namespace Microsoft.Extensions.DependencyInjection;

            public static class ServiceProviderServiceExtensions
            {
                public static T GetRequiredService<T>(global::System.IServiceProvider provider)
                {
                    global::System.ArgumentNullException.ThrowIfNull(provider);

                    var service = provider.GetService(typeof(T));
                    if (service is T typed)
                    {
                        return typed;
                    }

                    throw new global::System.InvalidOperationException($"No service for type '{typeof(T).FullName}'.");
                }
            }
            """;
        var designTimeCompilation = GeneratorTestHarness.CreateCompilation(source);
        var generatedSource = GeneratorTestHarness.RunGenerator(
            designTimeCompilation,
            "SharedKernel.Mediator.Generated.AppMediator.g.cs");
        var runtimeCompilation = GeneratorTestHarness.CreateCompilation(
            [
                """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using SharedKernel.Mediator;
                """ + Environment.NewLine + source,
                generatedSource,
                serviceProviderExtensionsSource,
            ],
            assemblyName: "SharedKernel.Mediator.Tests.GeneratedDispatchRuntime");
        var assembly = GeneratorTestHarness.LoadAssembly(runtimeCompilation);
        var requestType = assembly.GetType("Demo.LookupTour", throwOnError: true)!;
        var handlerType = assembly.GetType("Demo.LookupTourHandler", throwOnError: true)!;
        var mediatorType = assembly.GetType("SharedKernel.Mediator.AppMediator", throwOnError: true)!;
        var request = (IRequest<string>)Activator.CreateInstance(requestType, "vt-9000")!;
        var handler = Activator.CreateInstance(handlerType)!;
        var referenceDispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(requestType, typeof(string), handler)
            .Build();
        var generatedDispatcher = (IMediator)Activator.CreateInstance(
            mediatorType,
            new TestServiceProvider(handlerType, handler))!;

        // Act
        var referenceResponse = await referenceDispatcher.Send(request, CancellationToken.None);
        var generatedResponse = await generatedDispatcher.Send(request, CancellationToken.None);

        // Assert
        Assert.Equal(referenceResponse, generatedResponse);
    }

    private sealed class TestServiceProvider(Type registeredServiceType, object serviceInstance) : IServiceProvider
    {
        private readonly Type registeredServiceType = registeredServiceType;

        public object? GetService(Type serviceType)
        {
            return serviceType == registeredServiceType ? serviceInstance : null;
        }
    }
}
