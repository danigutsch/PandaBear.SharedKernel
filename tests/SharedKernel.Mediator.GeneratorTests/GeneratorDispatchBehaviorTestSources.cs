namespace SharedKernel.Mediator.GeneratorTests;

internal static class GeneratorDispatchBehaviorTestSources
{
    public static string StreamDispatchWithoutYieldTracing()
    {
        return """
            using SharedKernel.Mediator;
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

    public static string StreamDispatchWithYieldTracing()
    {
        return """
            using SharedKernel.Mediator;
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

    public static string StreamDispatchWithCancellationDuringEnumeration()
    {
        return """
            using SharedKernel.Mediator;
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

    public static string StreamDispatchWithEnumerationException()
    {
        return """
            using SharedKernel.Mediator;
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

    public static string StreamDispatchWithEarlyDisposal()
    {
        return """
            using SharedKernel.Mediator;
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

    public static string SendWithException()
    {
        return """
            using SharedKernel.Mediator;
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

    public static string PublishWithCancellation()
    {
        return """
            using SharedKernel.Mediator;
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

    public static string StreamDispatchWithCancellationNoPipelines()
    {
        return """
            using SharedKernel.Mediator;
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

    public static string StreamDispatchWithExceptionNoPipelines()
    {
        return """
            using SharedKernel.Mediator;
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

    public static string StreamDispatchNoPipelines()
    {
        return """
            using SharedKernel.Mediator;
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
