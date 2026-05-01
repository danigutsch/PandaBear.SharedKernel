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

    [Fact]
    public async Task Generated_Notification_Dispatch_Matches_Reference_Dispatcher_With_Exact_Type_Matching()
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

            public abstract record BaseNotification(string Name) : INotification;

            public sealed record DerivedNotification(string Name) : BaseNotification(Name);

            public sealed class BaseNotificationHandler : INotificationHandler<BaseNotification>
            {
                public ValueTask Handle(BaseNotification notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add($"base:{notification.Name}");
                    return ValueTask.CompletedTask;
                }
            }

            public sealed class DerivedNotificationHandlerOne : INotificationHandler<DerivedNotification>
            {
                public ValueTask Handle(DerivedNotification notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add($"derived-1:{notification.Name}");
                    return ValueTask.CompletedTask;
                }
            }

            public sealed class DerivedNotificationHandlerTwo : INotificationHandler<DerivedNotification>
            {
                public ValueTask Handle(DerivedNotification notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add($"derived-2:{notification.Name}");
                    return ValueTask.CompletedTask;
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var baseHandler = Activator.CreateInstance(runtime.GetRequiredType("Demo.BaseNotificationHandler"))!;
        var derivedHandlerOne = Activator.CreateInstance(runtime.GetRequiredType("Demo.DerivedNotificationHandlerOne"))!;
        var derivedHandlerTwo = Activator.CreateInstance(runtime.GetRequiredType("Demo.DerivedNotificationHandlerTwo"))!;
        var notification = (INotification)Activator.CreateInstance(runtime.GetRequiredType("Demo.DerivedNotification"), "tour")!;
        var referenceDispatcher = new ReferenceDispatcherBuilder()
            .AddNotificationHandler(runtime.GetRequiredType("Demo.BaseNotification"), baseHandler)
            .AddNotificationHandler(runtime.GetRequiredType("Demo.DerivedNotification"), derivedHandlerOne)
            .AddNotificationHandler(runtime.GetRequiredType("Demo.DerivedNotification"), derivedHandlerTwo)
            .Build();
        var generatedDispatcher = runtime.CreateMediator(baseHandler, derivedHandlerOne, derivedHandlerTwo);

        // Act
        await referenceDispatcher.Publish(notification, CancellationToken.None);
        var referenceTrace = runtime.ReadTraceEntries("Demo.TraceLog");
        runtime.ClearTraceEntries("Demo.TraceLog");
        await generatedDispatcher.Publish(notification, CancellationToken.None);
        var generatedTrace = runtime.ReadTraceEntries("Demo.TraceLog");

        // Assert
        Assert.Equal(referenceTrace, generatedTrace);
        Assert.Equal(
            [
                "derived-1:tour",
                "derived-2:tour",
            ],
            generatedTrace);
    }

    [Fact]
    public async Task Generated_Notification_Dispatch_With_Zero_Handlers_Completes_Without_Effects()
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

            public sealed record TourCreated(int Id) : INotification;
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var notification = (INotification)Activator.CreateInstance(runtime.GetRequiredType("Demo.TourCreated"), 42)!;
        var mediator = runtime.CreateMediator();

        // Act
        await mediator.Publish(notification, CancellationToken.None);

        // Assert
        Assert.Empty(runtime.ReadTraceEntries("Demo.TraceLog"));
    }

    [Fact]
    public async Task Generated_Notification_Dispatch_With_One_Handler_Invokes_Handler()
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

            public sealed record TourCreated(int Id) : INotification;

            public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add($"handler:{notification.Id}");
                    return ValueTask.CompletedTask;
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var handler = Activator.CreateInstance(runtime.GetRequiredType("Demo.TourCreatedHandler"))!;
        var notification = (INotification)Activator.CreateInstance(runtime.GetRequiredType("Demo.TourCreated"), 42)!;
        var mediator = runtime.CreateMediator(handler);

        // Act
        await mediator.Publish(notification, CancellationToken.None);
        var traceEntries = runtime.ReadTraceEntries("Demo.TraceLog");

        // Assert
        Assert.Equal(["handler:42"], traceEntries);
    }

    [Fact]
    public async Task Generated_Notification_Dispatch_Stops_On_First_Exception()
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

            public sealed record TourCreated(int Id) : INotification;

            public sealed class FailingHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add("before-fail");
                    throw new InvalidOperationException("boom");
                }
            }

            public sealed class SkippedHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add("after-fail");
                    return ValueTask.CompletedTask;
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var mediator = runtime.CreateMediator(
            Activator.CreateInstance(runtime.GetRequiredType("Demo.FailingHandler"))!,
            Activator.CreateInstance(runtime.GetRequiredType("Demo.SkippedHandler"))!);
        var notification = (INotification)Activator.CreateInstance(runtime.GetRequiredType("Demo.TourCreated"), 42)!;

        // Act
        async Task Act()
        {
            await mediator.Publish(notification, CancellationToken.None);
        }

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Equal("boom", exception.Message);
        Assert.Equal(["before-fail"], runtime.ReadTraceEntries("Demo.TraceLog"));
    }

    [Fact]
    public async Task Generated_Notification_Dispatch_Propagates_Cancellation_Token_To_Handlers()
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

            public sealed record TourCreated(int Id) : INotification;

            public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add($"handler:{ct.CanBeCanceled}");
                    return ValueTask.CompletedTask;
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var mediator = runtime.CreateMediator(Activator.CreateInstance(runtime.GetRequiredType("Demo.TourCreatedHandler"))!);
        var notification = (INotification)Activator.CreateInstance(runtime.GetRequiredType("Demo.TourCreated"), 42)!;
        using var cts = new CancellationTokenSource();

        // Act
        await mediator.Publish(notification, cts.Token);
        var traceEntries = runtime.ReadTraceEntries("Demo.TraceLog");

        // Assert
        Assert.Equal(["handler:True"], traceEntries);
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

        public void ClearTraceEntries(string typeName)
        {
            var traceType = GetRequiredType(typeName);
            var entries = (System.Collections.IList)traceType.GetProperty("Entries", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
            entries.Clear();
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
