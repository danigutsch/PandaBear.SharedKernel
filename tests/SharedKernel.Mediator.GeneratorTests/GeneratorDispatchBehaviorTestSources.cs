namespace PandaBear.Mediator.GeneratorTests;

/// <summary>C# source snippets for dispatch behavior tests, each defining requests, handlers and a static Demo.TraceLog that records execution order.</summary>
internal static class GeneratorDispatchBehaviorTestSources
{
    /// <summary>Stream request with a handler and validation and observability pipeline behaviors that trace only before and after their enumeration, not each yielded item.</summary>
    public static string StreamDispatchWithoutYieldTracing()
    {
        return """
            using PandaBear.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            [assembly: MediatorModule]

            namespace Demo;

            public static class TraceLog
            {
                public static List<string> Entries { get; } = [];
            }

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("handler-before");
                    for (var index = 1; index <= request.Count; index++)
                    {
                        await Task.Yield();
                        yield return $"item:{index}";
                    }

                    TraceLog.Entries.Add("handler-after");
                }
            }

            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("validation-before");
                    await foreach (var item in next().WithCancellation(ct))
                    {
                        yield return item;
                    }

                    TraceLog.Entries.Add("validation-after");
                }
            }

            [PipelineOrder(PipelineStage.Observability, Order = 10)]
            public sealed class ObservabilityBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("observability-before");
                    await foreach (var item in next().WithCancellation(ct))
                    {
                        yield return item;
                    }

                    TraceLog.Entries.Add("observability-after");
                }
            }
            """;
    }

    /// <summary>Stream request whose handler and both pipeline behaviors also trace every yielded item.</summary>
    public static string StreamDispatchWithYieldTracing()
    {
        return """
            using PandaBear.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            [assembly: MediatorModule]

            namespace Demo;

            public static class TraceLog
            {
                public static List<string> Entries { get; } = [];
            }

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("handler-before");
                    for (var index = 1; index <= request.Count; index++)
                    {
                        await Task.Yield();
                        TraceLog.Entries.Add($"handler-yield:{index}");
                        yield return $"item:{index}";
                    }

                    TraceLog.Entries.Add("handler-after");
                }
            }

            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("validation-before");
                    await foreach (var item in next().WithCancellation(ct))
                    {
                        TraceLog.Entries.Add($"validation-yield:{item}");
                        yield return item;
                    }

                    TraceLog.Entries.Add("validation-after");
                }
            }

            [PipelineOrder(PipelineStage.Observability, Order = 10)]
            public sealed class ObservabilityBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("observability-before");
                    await foreach (var item in next().WithCancellation(ct))
                    {
                        TraceLog.Entries.Add($"observability-yield:{item}");
                        yield return item;
                    }

                    TraceLog.Entries.Add("observability-after");
                }
            }
            """;
    }

    /// <summary>Stream request whose handler calls ct.ThrowIfCancellationRequested before each yield, with tracing in the handler and both pipeline behaviors.</summary>
    public static string StreamDispatchWithCancellationDuringEnumeration()
    {
        return """
            using PandaBear.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            [assembly: MediatorModule]

            namespace Demo;

            public static class TraceLog
            {
                public static List<string> Entries { get; } = [];
            }

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("handler-before");
                    for (var index = 1; index <= request.Count; index++)
                    {
                        await Task.Yield();
                        ct.ThrowIfCancellationRequested();
                        TraceLog.Entries.Add($"handler-yield:{index}");
                        yield return $"item:{index}";
                    }

                    TraceLog.Entries.Add("handler-after");
                }
            }

            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("validation-before");
                    await foreach (var item in next().WithCancellation(ct))
                    {
                        TraceLog.Entries.Add($"validation-yield:{item}");
                        yield return item;
                    }

                    TraceLog.Entries.Add("validation-after");
                }
            }

            [PipelineOrder(PipelineStage.Observability, Order = 10)]
            public sealed class ObservabilityBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("observability-before");
                    await foreach (var item in next().WithCancellation(ct))
                    {
                        TraceLog.Entries.Add($"observability-yield:{item}");
                        yield return item;
                    }

                    TraceLog.Entries.Add("observability-after");
                }
            }
            """;
    }

    /// <summary>Stream request whose handler yields item:1 and then throws InvalidOperationException 'boom', with tracing in the handler and both pipeline behaviors.</summary>
    public static string StreamDispatchWithEnumerationException()
    {
        return """
            using PandaBear.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            [assembly: MediatorModule]

            namespace Demo;

            public static class TraceLog
            {
                public static List<string> Entries { get; } = [];
            }

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("handler-before");
                    await Task.Yield();
                    TraceLog.Entries.Add("handler-yield:1");
                    yield return "item:1";
                    throw new InvalidOperationException("boom");
                }
            }

            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("validation-before");
                    await foreach (var item in next().WithCancellation(ct))
                    {
                        TraceLog.Entries.Add($"validation-yield:{item}");
                        yield return item;
                    }

                    TraceLog.Entries.Add("validation-after");
                }
            }

            [PipelineOrder(PipelineStage.Observability, Order = 10)]
            public sealed class ObservabilityBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("observability-before");
                    await foreach (var item in next().WithCancellation(ct))
                    {
                        TraceLog.Entries.Add($"observability-yield:{item}");
                        yield return item;
                    }

                    TraceLog.Entries.Add("observability-after");
                }
            }
            """;
    }

    /// <summary>Stream request whose handler records handler-disposed in a finally block, with tracing in the handler and both pipeline behaviors, to observe early disposal.</summary>
    public static string StreamDispatchWithEarlyDisposal()
    {
        return """
            using PandaBear.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            [assembly: MediatorModule]

            namespace Demo;

            public static class TraceLog
            {
                public static List<string> Entries { get; } = [];
            }

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("handler-before");
                    try
                    {
                        for (var index = 1; index <= request.Count; index++)
                        {
                            await Task.Yield();
                            TraceLog.Entries.Add($"handler-yield:{index}");
                            yield return $"item:{index}";
                        }
                    }
                    finally
                    {
                        TraceLog.Entries.Add("handler-disposed");
                    }
                }
            }

            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("validation-before");
                    try
                    {
                        await foreach (var item in next().WithCancellation(ct))
                        {
                            TraceLog.Entries.Add($"validation-yield:{item}");
                            yield return item;
                        }
                    }
                    finally
                    {
                        TraceLog.Entries.Add("validation-disposed");
                    }
                }
            }

            [PipelineOrder(PipelineStage.Observability, Order = 10)]
            public sealed class ObservabilityBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    TraceLog.Entries.Add("observability-before");
                    try
                    {
                        await foreach (var item in next().WithCancellation(ct))
                        {
                            TraceLog.Entries.Add($"observability-yield:{item}");
                            yield return item;
                        }
                    }
                    finally
                    {
                        TraceLog.Entries.Add("observability-disposed");
                    }
                }
            }
            """;
    }

    /// <summary>Request GetTour whose handler throws InvalidOperationException 'handler boom'.</summary>
    public static string SendWithException()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record GetTour(int Id) : IRequest<string>;

            public sealed class GetTourHandler : IRequestHandler<GetTour, string>
            {
                public ValueTask<string> Handle(GetTour request, CancellationToken ct)
                    => throw new InvalidOperationException("handler boom");
            }
            """;
    }

    /// <summary>Request GetTour whose handler returns 'tour:{Id}'.</summary>
    public static string SendSuccess()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record GetTour(int Id) : IRequest<string>;

            public sealed class GetTourHandler : IRequestHandler<GetTour, string>
            {
                public ValueTask<string> Handle(GetTour request, CancellationToken ct)
                    => ValueTask.FromResult($"tour:{request.Id}");
            }
            """;
    }

    /// <summary>Request GetTour whose handler throws InvalidOperationException internally, catches it and returns 'fallback'.</summary>
    public static string SendWithHandledException()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record GetTour(int Id) : IRequest<string>;

            public sealed class GetTourHandler : IRequestHandler<GetTour, string>
            {
                public ValueTask<string> Handle(GetTour request, CancellationToken ct)
                {
                    try
                    {
                        throw new InvalidOperationException("handler boom");
                    }
                    catch (InvalidOperationException)
                    {
                        return ValueTask.FromResult("fallback");
                    }
                }
            }
            """;
    }

    /// <summary>Request GetTour whose handler calls ct.ThrowIfCancellationRequested before returning 'tour:{Id}'.</summary>
    public static string SendWithCancellation()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record GetTour(int Id) : IRequest<string>;

            public sealed class GetTourHandler : IRequestHandler<GetTour, string>
            {
                public ValueTask<string> Handle(GetTour request, CancellationToken ct)
                {
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.FromResult($"tour:{request.Id}");
                }
            }
            """;
    }

    /// <summary>Request GetTour whose handler throws OperationCanceledException 'unexpected' without the caller's token being cancelled.</summary>
    public static string SendWithUnexpectedCancellation()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record GetTour(int Id) : IRequest<string>;

            public sealed class GetTourHandler : IRequestHandler<GetTour, string>
            {
                public ValueTask<string> Handle(GetTour request, CancellationToken ct)
                    => throw new OperationCanceledException("unexpected");
            }
            """;
    }

    /// <summary>Notification TourCreated whose single handler calls ct.ThrowIfCancellationRequested.</summary>
    public static string PublishWithCancellation()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record TourCreated(int Id) : INotification;

            public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                {
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.CompletedTask;
                }
            }
            """;
    }

    /// <summary>Notification TourCreated whose single handler throws OperationCanceledException 'unexpected'.</summary>
    public static string PublishWithUnexpectedCancellation()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record TourCreated(int Id) : INotification;

            public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                    => throw new OperationCanceledException("unexpected");
            }
            """;
    }

    /// <summary>Notification TourCreated whose single handler completes immediately.</summary>
    public static string PublishSuccess()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record TourCreated(int Id) : INotification;

            public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                    => ValueTask.CompletedTask;
            }
            """;
    }

    /// <summary>Notification TourCreated whose single handler throws InvalidOperationException 'handler boom'.</summary>
    public static string PublishWithException()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record TourCreated(int Id) : INotification;

            public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                    => throw new InvalidOperationException("handler boom");
            }
            """;
    }

    /// <summary>Notification TourCreated with two handlers ordered 10 and 20 that both complete immediately.</summary>
    public static string SequentialPublishSuccess()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record TourCreated(int Id) : INotification;

            [NotificationOrder(10)]
            public sealed class TourCreatedHandlerOne : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                    => ValueTask.CompletedTask;
            }

            [NotificationOrder(20)]
            public sealed class TourCreatedHandlerTwo : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                    => ValueTask.CompletedTask;
            }
            """;
    }

    /// <summary>Notification TourCreated with handlers ordered 10 and 20 where the first calls ct.ThrowIfCancellationRequested.</summary>
    public static string SequentialPublishWithCancellation()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record TourCreated(int Id) : INotification;

            [NotificationOrder(10)]
            public sealed class TourCreatedHandlerOne : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                {
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.CompletedTask;
                }
            }

            [NotificationOrder(20)]
            public sealed class TourCreatedHandlerTwo : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                    => ValueTask.CompletedTask;
            }
            """;
    }

    /// <summary>Notification TourCreated with handlers ordered 10 and 20 where the first throws InvalidOperationException 'handler boom'.</summary>
    public static string SequentialPublishWithException()
    {
        return """
            using PandaBear.Mediator;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record TourCreated(int Id) : INotification;

            [NotificationOrder(10)]
            public sealed class TourCreatedHandlerOne : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                    => throw new InvalidOperationException("handler boom");
            }

            [NotificationOrder(20)]
            public sealed class TourCreatedHandlerTwo : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct)
                    => ValueTask.CompletedTask;
            }
            """;
    }

    /// <summary>Stream request without pipelines whose handler checks cancellation before each yielded item.</summary>
    public static string StreamDispatchWithCancellationNoPipelines()
    {
        return """
            using PandaBear.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;

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
    }

    /// <summary>Stream request without pipelines whose handler yields item:1 and then throws InvalidOperationException 'boom'.</summary>
    public static string StreamDispatchWithExceptionNoPipelines()
    {
        return """
            using PandaBear.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    await Task.Yield();
                    yield return "item:1";
                    throw new InvalidOperationException("boom");
                }
            }
            """;
    }

    /// <summary>Stream request without pipelines whose handler yields item:1 through item:Count.</summary>
    public static string StreamDispatchNoPipelines()
    {
        return """
            using PandaBear.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;

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
                        yield return $"item:{index}";
                    }
                }
            }
            """;
    }
}
