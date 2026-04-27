using BenchmarkDotNet.Attributes;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures direct request-handler API shape costs across request forms and completion modes.
/// </summary>
[MemoryDiagnoser]
public class ApiShapeBenchmarks
{
    private const string ClassShape = "Class";
    private const string RecordClassShape = "RecordClass";
    private const string ReadonlyRecordStructShape = "ReadonlyRecordStruct";
    private const string SynchronousCompletion = "Synchronous";
    private const string AsynchronousCompletion = "Asynchronous";

    private Func<CancellationToken, ValueTask<int>> valueTaskHandler = null!;
    private Func<CancellationToken, Task<int>> taskHandler = null!;

    /// <summary>
    /// Gets or sets the request shape under test.
    /// </summary>
    [Params(ClassShape, RecordClassShape, ReadonlyRecordStructShape)]
    public string RequestShape { get; set; } = ClassShape;

    /// <summary>
    /// Gets or sets whether the handler completes synchronously or asynchronously.
    /// </summary>
    [Params(SynchronousCompletion, AsynchronousCompletion)]
    public string CompletionMode { get; set; } = SynchronousCompletion;

    /// <summary>
    /// Configures the delegates used by the benchmark scenarios.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var completesSynchronously = CompletionMode == SynchronousCompletion;

        switch (RequestShape)
        {
            case ClassShape:
                var classRequest = new ClassRequest(42);
                valueTaskHandler = ct => Handle(classRequest, completesSynchronously, ct);
                taskHandler = ct => HandleWithTask(classRequest, completesSynchronously, ct);
                break;

            case RecordClassShape:
                var recordClassRequest = new RecordClassRequest(42);
                valueTaskHandler = ct => Handle(recordClassRequest, completesSynchronously, ct);
                taskHandler = ct => HandleWithTask(recordClassRequest, completesSynchronously, ct);
                break;

            case ReadonlyRecordStructShape:
                var recordStructRequest = new ReadonlyRecordStructRequest(42);
                valueTaskHandler = ct => Handle(recordStructRequest, completesSynchronously, ct);
                taskHandler = ct => HandleWithTask(recordStructRequest, completesSynchronously, ct);
                break;

            default:
                throw new InvalidOperationException($"Unsupported request shape '{RequestShape}'.");
        }
    }

    /// <summary>
    /// Measures the direct mediator-style <see cref="ValueTask{TResult}"/> return path.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Baseline = true, Description = "Direct ValueTask<T> handler return")]
    public ValueTask<int> DirectValueTaskHandlerReturn()
    {
        return valueTaskHandler(CancellationToken.None);
    }

    /// <summary>
    /// Measures a benchmark-only <see cref="Task{TResult}"/> comparison helper for the same shape.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Description = "Direct Task<T> comparison helper")]
    public Task<int> DirectTaskComparisonHelper()
    {
        return taskHandler(CancellationToken.None);
    }

    private static ValueTask<int> Handle(ClassRequest request, bool completesSynchronously, CancellationToken ct)
    {
        return completesSynchronously ? ValueTask.FromResult(request.Id) : HandleAsync(request, ct);
    }

    private static ValueTask<int> Handle(RecordClassRequest request, bool completesSynchronously, CancellationToken ct)
    {
        return completesSynchronously ? ValueTask.FromResult(request.Id) : HandleAsync(request, ct);
    }

    private static ValueTask<int> Handle(ReadonlyRecordStructRequest request, bool completesSynchronously, CancellationToken ct)
    {
        return completesSynchronously ? ValueTask.FromResult(request.Id) : HandleAsync(request, ct);
    }

    private static Task<int> HandleWithTask(ClassRequest request, bool completesSynchronously, CancellationToken ct)
    {
        return completesSynchronously ? Task.FromResult(request.Id) : HandleWithTaskAsync(request, ct);
    }

    private static Task<int> HandleWithTask(RecordClassRequest request, bool completesSynchronously, CancellationToken ct)
    {
        return completesSynchronously ? Task.FromResult(request.Id) : HandleWithTaskAsync(request, ct);
    }

    private static Task<int> HandleWithTask(ReadonlyRecordStructRequest request, bool completesSynchronously, CancellationToken ct)
    {
        return completesSynchronously ? Task.FromResult(request.Id) : HandleWithTaskAsync(request, ct);
    }

    private static async ValueTask<int> HandleAsync(ClassRequest request, CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        return request.Id;
    }

    private static async ValueTask<int> HandleAsync(RecordClassRequest request, CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        return request.Id;
    }

    private static async ValueTask<int> HandleAsync(ReadonlyRecordStructRequest request, CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        return request.Id;
    }

    private static async Task<int> HandleWithTaskAsync(ClassRequest request, CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        return request.Id;
    }

    private static async Task<int> HandleWithTaskAsync(RecordClassRequest request, CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        return request.Id;
    }

    private static async Task<int> HandleWithTaskAsync(ReadonlyRecordStructRequest request, CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        return request.Id;
    }

    private sealed class ClassRequest(int id)
    {
        public int Id { get; } = id;
    }

    private sealed record RecordClassRequest(int Id);

    private readonly record struct ReadonlyRecordStructRequest(int Id);
}
