using BenchmarkDotNet.Attributes;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures a generated-style object dispatch switch separately from typed and generic dispatch.
/// </summary>
[MemoryDiagnoser]
public class ObjectDispatchBenchmarks
{
    private const string ClassShape = "Class";
    private const string RecordClassShape = "RecordClass";
    private const string ReadonlyRecordStructShape = "ReadonlyRecordStruct";

    private Func<CancellationToken, ValueTask<int>> typedDispatch = null!;
    private Func<CancellationToken, ValueTask<int>> genericDispatch = null!;
    private Func<CancellationToken, ValueTask<object?>> objectDispatch = null!;

    /// <summary>
    /// Gets or sets the request shape under test.
    /// </summary>
    [Params(ClassShape, RecordClassShape, ReadonlyRecordStructShape)]
    public string RequestShape { get; set; } = ClassShape;

    /// <summary>
    /// Configures the generated-style dispatch delegates used by the benchmarks.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        switch (RequestShape)
        {
            case ClassShape:
                var classRequest = new ClassRequest(42);
                typedDispatch = ct => BenchmarkMediator.Send(classRequest, ct);
                genericDispatch = ct => BenchmarkMediator.Send<int>(classRequest, ct);
                objectDispatch = ct => BenchmarkMediator.SendObject(classRequest, ct);
                break;

            case RecordClassShape:
                var recordClassRequest = new RecordClassRequest(42);
                typedDispatch = ct => BenchmarkMediator.Send(recordClassRequest, ct);
                genericDispatch = ct => BenchmarkMediator.Send<int>(recordClassRequest, ct);
                objectDispatch = ct => BenchmarkMediator.SendObject(recordClassRequest, ct);
                break;

            case ReadonlyRecordStructShape:
                var recordStructRequest = new ReadonlyRecordStructRequest(42);
                typedDispatch = ct => BenchmarkMediator.Send(recordStructRequest, ct);
                genericDispatch = ct => BenchmarkMediator.Send<int>(recordStructRequest, ct);
                objectDispatch = ct => BenchmarkMediator.SendObject(recordStructRequest, ct);
                break;

            default:
                throw new InvalidOperationException($"Unsupported request shape '{RequestShape}'.");
        }
    }

    /// <summary>
    /// Measures the direct typed generated overload.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Baseline = true, Description = "Generated typed overload")]
    public ValueTask<int> GeneratedTypedOverload()
    {
        return typedDispatch(CancellationToken.None);
    }

    /// <summary>
    /// Measures the generated generic switch path.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Description = "Generated generic switch dispatch")]
    public ValueTask<int> GeneratedGenericSwitch()
    {
        return genericDispatch(CancellationToken.None);
    }

    /// <summary>
    /// Measures the generated object switch path separately.
    /// </summary>
    /// <returns>The boxed handled response.</returns>
    [Benchmark(Description = "Generated object switch dispatch")]
    public ValueTask<object?> GeneratedObjectSwitch()
    {
        return objectDispatch(CancellationToken.None);
    }

    private static class BenchmarkMediator
    {
        public static ValueTask<int> Send(ClassRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(request.Id);
        }

        public static ValueTask<int> Send(RecordClassRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(request.Id);
        }

        public static ValueTask<int> Send(ReadonlyRecordStructRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(request.Id);
        }

        public static ValueTask<int> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)
        {
            return request switch
            {
                ClassRequest typed => Cast(Send(typed, ct)),
                RecordClassRequest typed => Cast(Send(typed, ct)),
                ReadonlyRecordStructRequest typed => Cast(Send(typed, ct)),
                _ => ValueTask.FromException<int>(
                    new NotSupportedException($"Generated request dispatch is not available for request type '{request.GetType().FullName}'.")),
            };
        }

        public static ValueTask<object?> SendObject(object request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            return request switch
            {
                ClassRequest typed => Box(Send(typed, ct)),
                RecordClassRequest typed => Box(Send(typed, ct)),
                ReadonlyRecordStructRequest typed => Box(Send(typed, ct)),
                _ => ValueTask.FromException<object?>(
                    new NotSupportedException($"Generated object dispatch is not available for request type '{request.GetType().FullName}'.")),
            };
        }

        private static async ValueTask<int> Cast(ValueTask<int> source)
        {
            return await source.ConfigureAwait(false);
        }

        private static async ValueTask<object?> Box(ValueTask<int> source)
        {
            return await source.ConfigureAwait(false);
        }
    }

    private sealed class ClassRequest(int id) : IRequest<int>
    {
        public int Id { get; } = id;
    }

    private sealed record RecordClassRequest(int Id) : IRequest<int>;

    private readonly record struct ReadonlyRecordStructRequest(int Id) : IRequest<int>;
}
