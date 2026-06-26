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
    public async Task Generated_Stream_Dispatch_Matches_Reference_Dispatcher()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Runtime.CompilerServices;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    for (var index = 1; index <= request.Count; index++)
                    {
                        await Task.Yield();
                        ct.ThrowIfCancellationRequested();
                        yield return $"item:{index}";
                    }
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var requestType = runtime.GetRequiredType("Demo.StreamTours");
        var handler = Activator.CreateInstance(runtime.GetRequiredType("Demo.StreamToursHandler"))!;
        var request = (IStreamRequest<string>)Activator.CreateInstance(requestType, 3)!;
        var referenceDispatcher = new ReferenceDispatcherBuilder()
            .AddStreamHandler(requestType, typeof(string), handler)
            .Build();
        var mediator = runtime.CreateMediator(handler);

        // Act
        var referenceItems = await AsyncEnumerableTestHelper.Collect(referenceDispatcher.Send(request, CancellationToken.None));
        var generatedItems = await AsyncEnumerableTestHelper.Collect(mediator.Send(request, CancellationToken.None));

        // Assert
        Assert.Equal(referenceItems, generatedItems);
        Assert.Equal(["item:1", "item:2", "item:3"], generatedItems);
    }

    [Fact]
    public async Task Generated_Client_Stream_Request_Dispatch_Matches_Reference_Dispatcher()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Runtime.CompilerServices;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record UploadTours(int Count) : IStreamCommand<int, string>
            {
                public IAsyncEnumerable<int> Items => CreateItems(Count);

                private static async IAsyncEnumerable<int> CreateItems(
                    int count,
                    [EnumeratorCancellation] CancellationToken ct = default)
                {
                    for (var index = 1; index <= count; index++)
                    {
                        await Task.Yield();
                        ct.ThrowIfCancellationRequested();
                        yield return index;
                    }
                }
            }

            public sealed class UploadToursHandler : IRequestHandler<UploadTours, string>
            {
                public async ValueTask<string> Handle(UploadTours request, CancellationToken ct)
                {
                    var total = 0;
                    await foreach (var item in request.Items.WithCancellation(ct))
                    {
                        total += item;
                    }

                    return total.ToString();
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var requestType = runtime.GetRequiredType("Demo.UploadTours");
        var handler = Activator.CreateInstance(runtime.GetRequiredType("Demo.UploadToursHandler"))!;
        var request = (IRequest<string>)Activator.CreateInstance(requestType, 3)!;
        var referenceDispatcher = new ReferenceDispatcherBuilder()
            .AddRequestHandler(requestType, typeof(string), handler)
            .Build();
        var mediator = runtime.CreateMediator(handler);

        // Act
        var referenceResponse = await referenceDispatcher.Send(request, CancellationToken.None);
        var generatedResponse = await mediator.Send(request, CancellationToken.None);

        // Assert
        Assert.Equal(referenceResponse, generatedResponse);
        Assert.Equal("6", generatedResponse);
    }

    [Fact]
    public async Task Generated_Duplex_Stream_Request_Dispatch_Matches_Reference_Dispatcher()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Runtime.CompilerServices;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record TransformTours(int Count) : IDuplexStreamCommand<int, string>
            {
                public IAsyncEnumerable<int> Items => CreateItems(Count);

                private static async IAsyncEnumerable<int> CreateItems(
                    int count,
                    [EnumeratorCancellation] CancellationToken ct = default)
                {
                    for (var index = 1; index <= count; index++)
                    {
                        await Task.Yield();
                        ct.ThrowIfCancellationRequested();
                        yield return index;
                    }
                }
            }

            public sealed class TransformToursHandler : IStreamRequestHandler<TransformTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    TransformTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    await foreach (var item in request.Items.WithCancellation(ct))
                    {
                        yield return $"item:{item}";
                    }
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var requestType = runtime.GetRequiredType("Demo.TransformTours");
        var handler = Activator.CreateInstance(runtime.GetRequiredType("Demo.TransformToursHandler"))!;
        var request = (IStreamRequest<string>)Activator.CreateInstance(requestType, 3)!;
        var referenceDispatcher = new ReferenceDispatcherBuilder()
            .AddStreamHandler(requestType, typeof(string), handler)
            .Build();
        var mediator = runtime.CreateMediator(handler);

        // Act
        var referenceItems = await AsyncEnumerableTestHelper.Collect(referenceDispatcher.Send(request, CancellationToken.None));
        var generatedItems = await AsyncEnumerableTestHelper.Collect(mediator.Send(request, CancellationToken.None));

        // Assert
        Assert.Equal(referenceItems, generatedItems);
        Assert.Equal(["item:1", "item:2", "item:3"], generatedItems);
    }

    [Fact]
    public async Task Generated_Stream_Dispatch_Executes_Stream_Pipelines_In_Stage_And_Order_Sequence()
    {
        // Arrange
        var source = GeneratorDispatchBehaviorTestSources.StreamDispatchWithoutYieldTracing();
        using var scenario = StreamDispatchTestScenario.Create(source, 2);

        // Act
        var (referenceItems, referenceTrace) = await GeneratorDispatchBehaviorTestsHelpers.CollectItemsAndTrace(
            scenario.SendReference(TestContext.Current.CancellationToken),
            scenario.ReadTrace);
        scenario.ClearTrace();
        var (generatedItems, generatedTrace) = await GeneratorDispatchBehaviorTestsHelpers.CollectItemsAndTrace(
            scenario.SendGenerated(TestContext.Current.CancellationToken),
            scenario.ReadTrace);

        // Assert
        Assert.Equal(referenceItems, generatedItems);
        Assert.Equal(["item:1", "item:2"], generatedItems);
        Assert.Equal(
            [
                "validation-before",
                "observability-before",
                "handler-before",
                "handler-after",
                "observability-after",
                "validation-after",
            ],
            referenceTrace);
        Assert.Equal(referenceTrace, generatedTrace);
    }

    [Fact]
    public async Task Generated_Stream_Dispatch_Yields_First_Item_Without_Full_Enumeration()
    {
        // Arrange
        var source = GeneratorDispatchBehaviorTestSources.StreamDispatchWithYieldTracing();
        using var scenario = StreamDispatchTestScenario.Create(source, 3);

        // Act
        var (referenceFirstItem, referenceTrace) = await GeneratorDispatchBehaviorTestsHelpers.ReadFirstItemAndTrace(
            scenario.SendReference(TestContext.Current.CancellationToken),
            scenario.ReadTrace,
            TestContext.Current.CancellationToken);
        scenario.ClearTrace();
        var (generatedFirstItem, generatedTrace) = await GeneratorDispatchBehaviorTestsHelpers.ReadFirstItemAndTrace(
            scenario.SendGenerated(TestContext.Current.CancellationToken),
            scenario.ReadTrace,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("item:1", referenceFirstItem);
        Assert.Equal(referenceFirstItem, generatedFirstItem);
        Assert.Equal(
            [
                "validation-before",
                "observability-before",
                "handler-before",
                "handler-yield:1",
                "observability-yield:item:1",
                "validation-yield:item:1",
            ],
            referenceTrace);
        Assert.Equal(referenceTrace, generatedTrace);
    }

    [Fact]
    public async Task Generated_Stream_Dispatch_Completes_Full_Enumeration_And_Finalization()
    {
        // Arrange
        var source = GeneratorDispatchBehaviorTestSources.StreamDispatchWithYieldTracing();
        using var scenario = StreamDispatchTestScenario.Create(source, 3);

        // Act
        var (referenceItems, referenceTrace) = await GeneratorDispatchBehaviorTestsHelpers.CollectItemsAndTrace(
            scenario.SendReference(TestContext.Current.CancellationToken),
            scenario.ReadTrace);
        scenario.ClearTrace();
        var (generatedItems, generatedTrace) = await GeneratorDispatchBehaviorTestsHelpers.CollectItemsAndTrace(
            scenario.SendGenerated(TestContext.Current.CancellationToken),
            scenario.ReadTrace);

        // Assert
        Assert.Equal(["item:1", "item:2", "item:3"], referenceItems);
        Assert.Equal(referenceItems, generatedItems);
        Assert.Equal(
            [
                "validation-before",
                "observability-before",
                "handler-before",
                "handler-yield:1",
                "observability-yield:item:1",
                "validation-yield:item:1",
                "handler-yield:2",
                "observability-yield:item:2",
                "validation-yield:item:2",
                "handler-yield:3",
                "observability-yield:item:3",
                "validation-yield:item:3",
                "handler-after",
                "observability-after",
                "validation-after",
            ],
            referenceTrace);
        Assert.Equal(referenceTrace, generatedTrace);
    }

    [Fact]
    public async Task Generated_Stream_Dispatch_Propagates_Cancellation_During_Enumeration()
    {
        // Arrange
        var source = GeneratorDispatchBehaviorTestSources.StreamDispatchWithCancellationDuringEnumeration();
        using var scenario = StreamDispatchTestScenario.Create(source, 3);

        // Act
        var (referenceFirstItem, referenceTrace) = await GeneratorDispatchBehaviorTestsHelpers.CancelAfterFirstItemAndTrace(
            scenario.SendReference,
            scenario.ReadTrace,
            TestContext.Current.CancellationToken);
        scenario.ClearTrace();
        var (generatedFirstItem, generatedTrace) = await GeneratorDispatchBehaviorTestsHelpers.CancelAfterFirstItemAndTrace(
            scenario.SendGenerated,
            scenario.ReadTrace,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("item:1", referenceFirstItem);
        Assert.Equal(referenceFirstItem, generatedFirstItem);
        Assert.Equal(
            [
                "validation-before",
                "observability-before",
                "handler-before",
                "handler-yield:1",
                "observability-yield:item:1",
                "validation-yield:item:1",
            ],
            referenceTrace);
        Assert.Equal(referenceTrace, generatedTrace);
    }

    [Fact]
    public async Task Generated_Stream_Dispatch_Propagates_Exceptions_During_Enumeration()
    {
        // Arrange
        var source = GeneratorDispatchBehaviorTestSources.StreamDispatchWithEnumerationException();
        using var scenario = StreamDispatchTestScenario.Create(source, 3);

        // Act
        var (referenceFirstItem, referenceTrace) = await GeneratorDispatchBehaviorTestsHelpers.ThrowAfterFirstItemAndTrace(
            scenario.SendReference(TestContext.Current.CancellationToken),
            scenario.ReadTrace,
            TestContext.Current.CancellationToken);
        scenario.ClearTrace();
        var (generatedFirstItem, generatedTrace) = await GeneratorDispatchBehaviorTestsHelpers.ThrowAfterFirstItemAndTrace(
            scenario.SendGenerated(TestContext.Current.CancellationToken),
            scenario.ReadTrace,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("item:1", referenceFirstItem);
        Assert.Equal(referenceFirstItem, generatedFirstItem);
        Assert.Equal(
            [
                "validation-before",
                "observability-before",
                "handler-before",
                "handler-yield:1",
                "observability-yield:item:1",
                "validation-yield:item:1",
            ],
            referenceTrace);
        Assert.Equal(referenceTrace, generatedTrace);
    }

    [Fact]
    public async Task Generated_Stream_Dispatch_Disposes_Pipeline_And_Handler_When_Enumeration_Stops_Early()
    {
        // Arrange
        var source = GeneratorDispatchBehaviorTestSources.StreamDispatchWithEarlyDisposal();
        using var scenario = StreamDispatchTestScenario.Create(source, 3);

        // Act
        var (referenceFirstItem, referenceTrace) = await GeneratorDispatchBehaviorTestsHelpers.ReadFirstItemThenDisposeAndTrace(
            scenario.SendReference(TestContext.Current.CancellationToken),
            scenario.ReadTrace,
            TestContext.Current.CancellationToken);
        scenario.ClearTrace();
        var (generatedFirstItem, generatedTrace) = await GeneratorDispatchBehaviorTestsHelpers.ReadFirstItemThenDisposeAndTrace(
            scenario.SendGenerated(TestContext.Current.CancellationToken),
            scenario.ReadTrace,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("item:1", referenceFirstItem);
        Assert.Equal(referenceFirstItem, generatedFirstItem);
        Assert.Equal(
            [
                "validation-before",
                "observability-before",
                "handler-before",
                "handler-yield:1",
                "observability-yield:item:1",
                "validation-yield:item:1",
                "handler-disposed",
                "observability-disposed",
                "validation-disposed",
            ],
            referenceTrace);
        Assert.Equal(referenceTrace, generatedTrace);
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

            [NotificationOrder(20)]
            public sealed class DerivedNotificationHandlerOne : INotificationHandler<DerivedNotification>
            {
                public ValueTask Handle(DerivedNotification notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add($"derived-1:{notification.Name}");
                    return ValueTask.CompletedTask;
                }
            }

            [NotificationOrder(10)]
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
                "derived-2:tour",
                "derived-1:tour",
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
    public async Task Generated_Notification_Dispatch_Uses_Per_Notification_Strategy()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public static class TraceLog
            {
                public static List<string> Entries { get; } = [];
            }

            public static class ParallelGate
            {
                private static int started;
                private static readonly TaskCompletionSource<bool> AllStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

                public static async ValueTask Enter(string name, CancellationToken ct)
                {
                    TraceLog.Entries.Add($"start:{name}");
                    if (Interlocked.Increment(ref started) == 2)
                    {
                        AllStarted.TrySetResult(true);
                    }

                    await AllStarted.Task.WaitAsync(ct);
                    TraceLog.Entries.Add($"end:{name}");
                }
            }

            public sealed record SequentialNotification(int Id) : INotification;

            [NotificationDispatch(NotificationDispatchStrategy.Parallel)]
            public sealed record ParallelNotification(int Id) : INotification;

            [NotificationOrder(20)]
            public sealed class SequentialHandlerOne : INotificationHandler<SequentialNotification>
            {
                public ValueTask Handle(SequentialNotification notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add("sequential-1");
                    return ValueTask.CompletedTask;
                }
            }

            [NotificationOrder(10)]
            public sealed class SequentialHandlerTwo : INotificationHandler<SequentialNotification>
            {
                public ValueTask Handle(SequentialNotification notification, CancellationToken ct)
                {
                    TraceLog.Entries.Add("sequential-2");
                    return ValueTask.CompletedTask;
                }
            }

            public sealed class ParallelHandlerOne : INotificationHandler<ParallelNotification>
            {
                public ValueTask Handle(ParallelNotification notification, CancellationToken ct)
                {
                    return ParallelGate.Enter("parallel-1", ct);
                }
            }

            public sealed class ParallelHandlerTwo : INotificationHandler<ParallelNotification>
            {
                public ValueTask Handle(ParallelNotification notification, CancellationToken ct)
                {
                    return ParallelGate.Enter("parallel-2", ct);
                }
            }
            """;
        using var runtime = GeneratedMediatorRuntimeContext.Create(source);
        var mediator = runtime.CreateMediator(
            Activator.CreateInstance(runtime.GetRequiredType("Demo.SequentialHandlerOne"))!,
            Activator.CreateInstance(runtime.GetRequiredType("Demo.SequentialHandlerTwo"))!,
            Activator.CreateInstance(runtime.GetRequiredType("Demo.ParallelHandlerOne"))!,
            Activator.CreateInstance(runtime.GetRequiredType("Demo.ParallelHandlerTwo"))!);
        var sequentialNotification = (INotification)Activator.CreateInstance(
            runtime.GetRequiredType("Demo.SequentialNotification"),
            42)!;
        var parallelNotification = (INotification)Activator.CreateInstance(
            runtime.GetRequiredType("Demo.ParallelNotification"),
            43)!;

        // Act
        await mediator.Publish(sequentialNotification, CancellationToken.None);
        var sequentialTrace = runtime.ReadTraceEntries("Demo.TraceLog");
        runtime.ClearTraceEntries("Demo.TraceLog");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await mediator.Publish(parallelNotification, cts.Token);
        var parallelTrace = runtime.ReadTraceEntries("Demo.TraceLog");

        // Assert
        Assert.Equal(["sequential-2", "sequential-1"], sequentialTrace);
        Assert.Equal(4, parallelTrace.Length);
        Assert.All(parallelTrace[..2], static entry => Assert.StartsWith("start:", entry, StringComparison.Ordinal));
        Assert.All(parallelTrace[2..], static entry => Assert.StartsWith("end:", entry, StringComparison.Ordinal));
        Assert.Contains("start:parallel-1", parallelTrace, StringComparer.Ordinal);
        Assert.Contains("start:parallel-2", parallelTrace, StringComparer.Ordinal);
        Assert.Contains("end:parallel-1", parallelTrace, StringComparer.Ordinal);
        Assert.Contains("end:parallel-2", parallelTrace, StringComparer.Ordinal);
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
        var traceEntries = runtime.ReadTraceEntries("Demo.TraceLog");
        Assert.Equal("boom", exception.Message);
        Assert.Equal(["before-fail"], traceEntries);
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

}
