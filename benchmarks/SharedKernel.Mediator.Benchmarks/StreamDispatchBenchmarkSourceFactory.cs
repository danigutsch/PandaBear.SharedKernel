using System.Globalization;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Creates synthetic stream-dispatch benchmark sources for generated-style mediator comparisons.
/// </summary>
internal static class StreamDispatchBenchmarkSourceFactory
{
    /// <summary>
    /// Creates benchmark source with the requested stream-item count.
    /// </summary>
    /// <param name="itemCount">The number of streamed items to emit.</param>
    /// <returns>The synthetic stream-dispatch benchmark source file.</returns>
    public static string CreateSource(int itemCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);

        return $$"""
            #nullable enable

            using SharedKernel.Mediator;
            using System.Runtime.CompilerServices;
            using System.Threading.Channels;

            namespace BenchmarkApp;

            public sealed record BenchmarkStreamRequest(int ItemCount) : IStreamRequest<int>;

            public sealed class BenchmarkStreamHandler : IStreamRequestHandler<BenchmarkStreamRequest, int>
            {
                public IAsyncEnumerable<int> Handle(BenchmarkStreamRequest request, CancellationToken ct)
                {
                    return new RangeAsyncEnumerable(request.ItemCount, ct);
                }
            }

            public sealed class BenchmarkAppMediator : IMediator
            {
                private readonly BenchmarkStreamHandler handler = new();

                public IAsyncEnumerable<int> Send(BenchmarkStreamRequest request, CancellationToken ct)
                {
                    return handler.Handle(request, ct);
                }

                public IAsyncEnumerable<TResponse> Send<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct)
                {
                    ArgumentNullException.ThrowIfNull(request);

                    return request switch
                    {
                        BenchmarkStreamRequest typed => GeneratedDispatch.Send<TResponse>(this, typed, ct),
                        _ => GeneratedDispatch.ThrowNoStreamHandler<TResponse>(request),
                    };
                }

                public IAsyncEnumerable<int> SendViaChannel(BenchmarkStreamRequest request, CancellationToken ct)
                {
                    return GeneratedDispatch.SendViaChannel(this, request, ct);
                }

                public IAsyncEnumerable<int> SendViaBufferedCopy(BenchmarkStreamRequest request, CancellationToken ct)
                {
                    return GeneratedDispatch.SendViaBufferedCopy(this, request, ct);
                }

                public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)
                {
                    ArgumentNullException.ThrowIfNull(request);
                    throw new NotSupportedException("Benchmark unary dispatch is not implemented.");
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
            }

            internal static class GeneratedDispatch
            {
                public static IAsyncEnumerable<TResponse> Send<TResponse>(
                    BenchmarkAppMediator mediator,
                    BenchmarkStreamRequest request,
                    CancellationToken ct)
                {
                    return CastStream<int, TResponse>(mediator.Send(request, ct), ct);
                }

                public static IAsyncEnumerable<int> SendViaChannel(
                    BenchmarkAppMediator mediator,
                    BenchmarkStreamRequest request,
                    CancellationToken ct)
                {
                    return CopyViaChannel(mediator.Send(request, ct), ct);
                }

                public static IAsyncEnumerable<int> SendViaBufferedCopy(
                    BenchmarkAppMediator mediator,
                    BenchmarkStreamRequest request,
                    CancellationToken ct)
                {
                    return BufferBeforeYield(mediator.Send(request, ct), ct);
                }

                private static async IAsyncEnumerable<TTarget> CastStream<TSource, TTarget>(
                    IAsyncEnumerable<TSource> source,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                    {
                        if (item is TTarget typed)
                        {
                            yield return typed;
                            continue;
                        }

                        throw new InvalidOperationException(
                            $"Benchmark stream dispatch returned '{typeof(TSource).FullName}' when '{typeof(TTarget).FullName}' was expected.");
                    }
                }

                private static IAsyncEnumerable<int> CopyViaChannel(
                    IAsyncEnumerable<int> source,
                    CancellationToken ct)
                {
                    var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
                    {
                        SingleReader = true,
                        SingleWriter = true,
                        AllowSynchronousContinuations = true,
                        FullMode = BoundedChannelFullMode.Wait,
                    });

                    _ = PumpAsync(source, channel.Writer, ct);
                    return ReadChannel(channel.Reader, ct);
                }

                private static async Task PumpAsync(
                    IAsyncEnumerable<int> source,
                    ChannelWriter<int> writer,
                    CancellationToken ct)
                {
                    Exception? failure = null;

                    try
                    {
                        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                        {
                            await writer.WriteAsync(item, ct).ConfigureAwait(false);
                        }
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                    finally
                    {
                        writer.TryComplete(failure);
                    }
                }

                private static async IAsyncEnumerable<int> ReadChannel(
                    ChannelReader<int> reader,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
                    {
                        while (reader.TryRead(out var item))
                        {
                            yield return item;
                        }
                    }
                }

                private static async IAsyncEnumerable<int> BufferBeforeYield(
                    IAsyncEnumerable<int> source,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    List<int> buffer = [];

                    await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                    {
                        buffer.Add(item);
                    }

                    foreach (var item in buffer)
                    {
                        ct.ThrowIfCancellationRequested();
                        yield return item;
                    }
                }

                internal static IAsyncEnumerable<TResponse> ThrowNoStreamHandler<TResponse>(IStreamRequest<TResponse> request)
                {
                    throw new NotSupportedException(
                        $"Benchmark stream dispatch is not available for request type '{request.GetType().FullName}'.");
                }
            }

            internal static class StreamConsumers
            {
                public static async ValueTask<int> ReadFirstItemAsync(IAsyncEnumerable<int> source, CancellationToken ct)
                {
                    await using var enumerator = source.GetAsyncEnumerator(ct);
                    return await enumerator.MoveNextAsync().ConfigureAwait(false) ? enumerator.Current : 0;
                }

                public static async ValueTask<int> EnumerateAllAsync(IAsyncEnumerable<int> source, CancellationToken ct)
                {
                    var count = 0;
                    await using var enumerator = source.GetAsyncEnumerator(ct);

                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        count++;
                    }

                    return count;
                }
            }

            internal sealed class RangeAsyncEnumerable : IAsyncEnumerable<int>
            {
                private readonly int count;
                private readonly CancellationToken cancellationToken;

                public RangeAsyncEnumerable(int count, CancellationToken cancellationToken)
                {
                    this.count = count;
                    this.cancellationToken = cancellationToken;
                }

                public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
                {
                    return new Enumerator(count, CancellationTokenSource.CreateLinkedTokenSource(this.cancellationToken, cancellationToken).Token);
                }

                private sealed class Enumerator : IAsyncEnumerator<int>
                {
                    private readonly int count;
                    private readonly CancellationToken cancellationToken;
                    private int index;

                    public Enumerator(int count, CancellationToken cancellationToken)
                    {
                        this.count = count;
                        this.cancellationToken = cancellationToken;
                    }

                    public int Current { get; private set; }

                    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

                    public ValueTask<bool> MoveNextAsync()
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (index >= count)
                        {
                            return ValueTask.FromResult(false);
                        }

                        index++;
                        Current = index;
                        return ValueTask.FromResult(true);
                    }
                }
            }

            public static class BenchmarkExports
            {
                public static Func<CancellationToken, ValueTask<int>> CreateDirectStreamHandlerFirstItem()
                {
                    var handler = new BenchmarkStreamHandler();
                    var request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.ReadFirstItemAsync(handler.Handle(request, ct), ct);
                }

                public static Func<CancellationToken, ValueTask<int>> CreateGeneratedMediatorDirectReturnFirstItem()
                {
                    var mediator = new BenchmarkAppMediator();
                    var request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.ReadFirstItemAsync(mediator.Send(request, ct), ct);
                }

                public static Func<CancellationToken, ValueTask<int>> CreateGeneratedWrapperFirstItem()
                {
                    var mediator = new BenchmarkAppMediator();
                    IStreamRequest<int> request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.ReadFirstItemAsync(mediator.Send(request, ct), ct);
                }

                public static Func<CancellationToken, ValueTask<int>> CreateChannelCopyFirstItem()
                {
                    var mediator = new BenchmarkAppMediator();
                    var request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.ReadFirstItemAsync(mediator.SendViaChannel(request, ct), ct);
                }

                public static Func<CancellationToken, ValueTask<int>> CreateBufferedCopyFirstItem()
                {
                    var mediator = new BenchmarkAppMediator();
                    var request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.ReadFirstItemAsync(mediator.SendViaBufferedCopy(request, ct), ct);
                }

                public static Func<CancellationToken, ValueTask<int>> CreateDirectStreamHandlerEnumeration()
                {
                    var handler = new BenchmarkStreamHandler();
                    var request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.EnumerateAllAsync(handler.Handle(request, ct), ct);
                }

                public static Func<CancellationToken, ValueTask<int>> CreateGeneratedMediatorDirectReturnEnumeration()
                {
                    var mediator = new BenchmarkAppMediator();
                    var request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.EnumerateAllAsync(mediator.Send(request, ct), ct);
                }

                public static Func<CancellationToken, ValueTask<int>> CreateGeneratedWrapperEnumeration()
                {
                    var mediator = new BenchmarkAppMediator();
                    IStreamRequest<int> request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.EnumerateAllAsync(mediator.Send(request, ct), ct);
                }

                public static Func<CancellationToken, ValueTask<int>> CreateChannelCopyEnumeration()
                {
                    var mediator = new BenchmarkAppMediator();
                    var request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.EnumerateAllAsync(mediator.SendViaChannel(request, ct), ct);
                }

                public static Func<CancellationToken, ValueTask<int>> CreateBufferedCopyEnumeration()
                {
                    var mediator = new BenchmarkAppMediator();
                    var request = new BenchmarkStreamRequest({{itemCount.ToString(CultureInfo.InvariantCulture)}});
                    return ct => StreamConsumers.EnumerateAllAsync(mediator.SendViaBufferedCopy(request, ct), ct);
                }
            }
            """;
    }
}
