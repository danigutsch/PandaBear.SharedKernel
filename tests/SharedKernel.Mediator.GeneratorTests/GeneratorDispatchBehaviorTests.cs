using SharedKernel.Mediator.Testing.ReferenceDispatcher;
using System.Reflection;

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
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var requestType = runtime.GetRequiredType("Demo.LookupTour");
        var handlerType = runtime.GetRequiredType("Demo.LookupTourHandler");
        var request = (IRequest<string>)Activator.CreateInstance(requestType, "vt-9000")!;
        var handler = Activator.CreateInstance(handlerType)!;
        var referenceDispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(requestType, typeof(string), handler)
            .Build();
        var generatedDispatcher = runtime.CreateMediator(handler);

        // Act
        var referenceResponse = await referenceDispatcher.Send(request, CancellationToken.None);
        var generatedResponse = await generatedDispatcher.Send(request, CancellationToken.None);

        // Assert
        Assert.Equal(referenceResponse, generatedResponse);
    }

    [Fact]
    public async Task Generated_Request_Dispatch_Executes_Pipelines_In_Stage_And_Order_Sequence()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;

            [assembly: MediatorModule]

            namespace Demo;

            public static class TraceLog
            {
                public static List<string> Entries { get; } = [];
            }

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct)
                {
                    TraceLog.Entries.Add("handler");
                    return ValueTask.FromResult(42);
                }
            }

            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationBehavior : IPipelineBehavior<CreateTour, int>
            {
                public async ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct)
                {
                    TraceLog.Entries.Add("validation-before");
                    var response = await next();
                    TraceLog.Entries.Add("validation-after");
                    return response;
                }
            }

            [PipelineOrder(PipelineStage.Authorization, Order = 10)]
            public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct)
                {
                    TraceLog.Entries.Add("authorization-before");
                    var response = await next();
                    TraceLog.Entries.Add("authorization-after");
                    return response;
                }
            }

            [PipelineOrder(PipelineStage.Transaction, Order = 15)]
            public sealed class TransactionBehavior : IPipelineBehavior<CreateTour, int>
            {
                public async ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct)
                {
                    TraceLog.Entries.Add("transaction-before");
                    var response = await next();
                    TraceLog.Entries.Add("transaction-after");
                    return response;
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var requestType = runtime.GetRequiredType("Demo.CreateTour");
        var handler = Activator.CreateInstance(runtime.GetRequiredType("Demo.CreateTourHandler"))!;
        var validation = Activator.CreateInstance(runtime.GetRequiredType("Demo.ValidationBehavior"))!;
        var authorization = Activator.CreateInstance(runtime.GetRequiredType("Demo.AuthorizationBehavior`2").MakeGenericType(requestType, typeof(int)))!;
        var transaction = Activator.CreateInstance(runtime.GetRequiredType("Demo.TransactionBehavior"))!;
        var mediator = runtime.CreateMediator(handler, validation, authorization, transaction);
        var request = (IRequest<int>)Activator.CreateInstance(requestType, "Tour")!;

        // Act
        var response = await mediator.Send(request, CancellationToken.None);
        var traceEntries = runtime.ReadTraceEntries("Demo.TraceLog");

        // Assert
        Assert.Equal(42, response);
        Assert.Equal(
            [
                "validation-before",
                "authorization-before",
                "transaction-before",
                "handler",
                "transaction-after",
                "authorization-after",
                "validation-after",
            ],
            traceEntries);
    }

    [Fact]
    public async Task Generated_Request_Dispatch_Propagates_Pipeline_Exceptions()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }

            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class FailingBehavior : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct)
                {
                    throw new InvalidOperationException("boom");
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var requestType = runtime.GetRequiredType("Demo.CreateTour");
        var mediator = runtime.CreateMediator(
            Activator.CreateInstance(runtime.GetRequiredType("Demo.CreateTourHandler"))!,
            Activator.CreateInstance(runtime.GetRequiredType("Demo.FailingBehavior"))!);
        var request = (IRequest<int>)Activator.CreateInstance(requestType, "Tour")!;

        // Act
        async Task Act()
        {
            await mediator.Send(request, CancellationToken.None);
        }

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task Generated_Request_Dispatch_Propagates_Cancellation_Token_To_Pipelines_And_Handler()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;

            [assembly: MediatorModule]

            namespace Demo;

            public static class TraceLog
            {
                public static List<string> Entries { get; } = [];
            }

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct)
                {
                    TraceLog.Entries.Add($"handler:{ct.CanBeCanceled}");
                    return ValueTask.FromResult(42);
                }
            }

            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationBehavior : IPipelineBehavior<CreateTour, int>
            {
                public async ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct)
                {
                    TraceLog.Entries.Add($"pipeline:{ct.CanBeCanceled}");
                    return await next();
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var requestType = runtime.GetRequiredType("Demo.CreateTour");
        var mediator = runtime.CreateMediator(
            Activator.CreateInstance(runtime.GetRequiredType("Demo.CreateTourHandler"))!,
            Activator.CreateInstance(runtime.GetRequiredType("Demo.ValidationBehavior"))!);
        var request = (IRequest<int>)Activator.CreateInstance(requestType, "Tour")!;
        using var cts = new CancellationTokenSource();

        // Act
        var response = await mediator.Send(request, cts.Token);
        var traceEntries = runtime.ReadTraceEntries("Demo.TraceLog");

        // Assert
        Assert.Equal(42, response);
        Assert.Equal(
            [
                "pipeline:True",
                "handler:True",
            ],
            traceEntries);
    }

    private sealed class GeneratedMediatorRuntimeContext : IDisposable
    {
        private readonly Assembly assembly;
        private readonly Type mediatorType;

        private GeneratedMediatorRuntimeContext(Assembly assembly, Type mediatorType)
        {
            this.assembly = assembly;
            this.mediatorType = mediatorType;
        }

        public static GeneratedMediatorRuntimeContext Create(string source)
        {
            const string runtimeUsings = """
                using System;
                using System.Collections.Generic;
                using System.Threading;
                using System.Threading.Tasks;

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
            var runResult = GeneratorTestHarness.RunGeneratorDriver(designTimeCompilation);
            var generatedMediatorSource = GeneratorTestHarness.GetGeneratedSource(
                runResult,
                "SharedKernel.Mediator.Generated.AppMediator.g.cs");
            var generatedDispatchSource = GeneratorTestHarness.GetGeneratedSource(
                runResult,
                "SharedKernel.Mediator.Generated.GeneratedDispatch.g.cs");
            var generatedPipelinesSource = GeneratorTestHarness.GetGeneratedSource(
                runResult,
                "SharedKernel.Mediator.Generated.GeneratedPipelines.g.cs");
            var runtimeCompilation = GeneratorTestHarness.CreateCompilation(
                [runtimeUsings + source, generatedMediatorSource, generatedDispatchSource, generatedPipelinesSource, serviceProviderExtensionsSource],
                assemblyName: "SharedKernel.Mediator.Tests.GeneratedDispatchRuntime");
            var assembly = GeneratorTestHarness.LoadAssembly(runtimeCompilation);
            var mediatorType = assembly.GetType("SharedKernel.Mediator.AppMediator", throwOnError: true)!;

            return new GeneratedMediatorRuntimeContext(assembly, mediatorType);
        }

        public Type GetRequiredType(string typeName)
        {
            return assembly.GetType(typeName, throwOnError: true)!;
        }

        public IMediator CreateMediator(params object[] services)
        {
            return (IMediator)Activator.CreateInstance(mediatorType, new TestServiceProvider(services))!;
        }

        public string[] ReadTraceEntries(string typeName)
        {
            var traceType = GetRequiredType(typeName);
            var entries = (IReadOnlyList<string>)traceType.GetProperty("Entries", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
            return [.. entries];
        }

        public void Dispose()
        {
            if (assembly is IDisposable disposableAssembly)
            {
                disposableAssembly.Dispose();
            }
        }
    }

    private sealed class TestServiceProvider(params object[] services) : IServiceProvider
    {
        private readonly Dictionary<Type, object> services = services.ToDictionary(static service => service.GetType());

        public object? GetService(Type serviceType)
        {
            return services.TryGetValue(serviceType, out var service) ? service : null;
        }
    }
}
