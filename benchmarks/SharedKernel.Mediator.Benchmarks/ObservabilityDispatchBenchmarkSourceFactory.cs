namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Creates synthetic dispatch benchmark sources for optional mediator observability comparisons.
/// </summary>
internal static class ObservabilityDispatchBenchmarkSourceFactory
{
    /// <summary>
    /// The synthetic benchmark source for the observability dispatch scenarios.
    /// </summary>
    public const string Source = """
        #nullable enable

        using SharedKernel.Mediator;

        namespace BenchmarkApp;

        public sealed record BenchmarkRequest(int Id) : IQuery<int>;

        public sealed class BenchmarkRequestHandler : IQueryHandler<BenchmarkRequest, int>
        {
            public ValueTask<int> Handle(BenchmarkRequest request, CancellationToken ct)
            {
                return ValueTask.FromResult(request.Id + 1);
            }
        }

        public sealed class BenchmarkAppMediator : IMediator
        {
            private readonly BenchmarkRequestHandler handler = new();
            private readonly ActivityBehavior<BenchmarkRequest, int> activityBehavior = new();

            public ValueTask<int> SendWithoutObservability(BenchmarkRequest request, CancellationToken ct)
            {
                return handler.Handle(request, ct);
            }

            public ValueTask<int> SendWithObservability(BenchmarkRequest request, CancellationToken ct)
            {
                return activityBehavior.Handle(request, () => handler.Handle(request, ct), ct);
            }

            public ValueTask<int> Send(BenchmarkRequest request, CancellationToken ct)
            {
                return SendWithoutObservability(request, ct);
            }

            public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)
            {
                ArgumentNullException.ThrowIfNull(request);

                return request switch
                {
                    BenchmarkRequest typed => Cast<int, TResponse>(Send(typed, ct)),
                    _ => ThrowNoHandler(request),
                };
            }

            public ValueTask<object?> SendObject(object request, CancellationToken ct)
            {
                ArgumentNullException.ThrowIfNull(request);
                throw new NotSupportedException("Benchmark object dispatch is not implemented.");
            }

            public ValueTask Publish<TNotification>(TNotification notification, CancellationToken ct)
                where TNotification : INotification
            {
                ArgumentNullException.ThrowIfNull(notification);
                throw new NotSupportedException("Benchmark notification dispatch is not implemented.");
            }

            public IAsyncEnumerable<TResponse> Send<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct)
            {
                ArgumentNullException.ThrowIfNull(request);
                throw new NotSupportedException("Benchmark stream dispatch is not implemented.");
            }

            private static async ValueTask<TTarget> Cast<TSource, TTarget>(ValueTask<TSource> source)
            {
                var result = await source.ConfigureAwait(false);
                if (result is TTarget typed)
                {
                    return typed;
                }

                throw new InvalidOperationException($"Benchmark mediator returned '{typeof(TSource).FullName}' when '{typeof(TTarget).FullName}' was expected.");
            }

            private static ValueTask<TResponse> ThrowNoHandler<TResponse>(IRequest<TResponse> request)
            {
                throw new NotSupportedException($"Benchmark mediator dispatch is not available for request type '{request.GetType().FullName}'.");
            }
        }

        public static class BenchmarkExports
        {
            public static Func<CancellationToken, ValueTask<int>> CreateWithoutObservability()
            {
                var mediator = new BenchmarkAppMediator();
                var request = new BenchmarkRequest(42);
                return ct => mediator.SendWithoutObservability(request, ct);
            }

            public static Func<CancellationToken, ValueTask<int>> CreateWithObservability()
            {
                var mediator = new BenchmarkAppMediator();
                var request = new BenchmarkRequest(42);
                return ct => mediator.SendWithObservability(request, ct);
            }
        }
        """;
}
