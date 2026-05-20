using BenchmarkDotNet.Attributes;

namespace SharedKernel.Functional.Benchmarks;

/// <summary>
/// Measures whether passing a larger payload by <c>in</c> changes hot-path helper cost enough to justify API experiments.
/// </summary>
[MemoryDiagnoser]
public class LargeStructInParameterBenchmarks
{
    private readonly LargeStructBenchmarkPayload payload = new(1, 2, 3, 4, 5, 6, 7, 8);
    private readonly Result<LargeStructBenchmarkPayload> result;

    /// <summary>
    /// Initializes a new instance of the <see cref="LargeStructInParameterBenchmarks"/> class.
    /// </summary>
    public LargeStructInParameterBenchmarks()
    {
        result = Result.Ok(payload);
    }

    /// <summary>
    /// Measures creating a success result through a by-value helper.
    /// </summary>
    /// <returns>The created result.</returns>
    [Benchmark(Baseline = true, Description = "Create helper by value")]
    public Result<LargeStructBenchmarkPayload> CreateByValue()
    {
        return Helpers.CreateByValue(payload);
    }

    /// <summary>
    /// Measures creating a success result through an <c>in</c> helper.
    /// </summary>
    /// <returns>The created result.</returns>
    [Benchmark(Description = "Create helper by in")]
    public Result<LargeStructBenchmarkPayload> CreateByIn()
    {
        return Helpers.CreateByIn(in payload);
    }

    /// <summary>
    /// Measures projecting a result through a by-value helper.
    /// </summary>
    /// <returns>The projected sum.</returns>
    [Benchmark(Description = "Map-style helper by value")]
    public int ProjectByValue()
    {
        return Helpers.ProjectByValue(payload);
    }

    /// <summary>
    /// Measures projecting a result through an <c>in</c> helper.
    /// </summary>
    /// <returns>The projected sum.</returns>
    [Benchmark(Description = "Map-style helper by in")]
    public int ProjectByIn()
    {
        return Helpers.ProjectByIn(in payload);
    }

    /// <summary>
    /// Measures reading the result value into a by-value helper.
    /// </summary>
    /// <returns>The extracted sum.</returns>
    [Benchmark(Description = "TryGetValue sink by value")]
    public int TryGetValueSinkByValue()
    {
        result.TryGetValue(out var currentPayload);
        return Helpers.ProjectByValue(currentPayload);
    }

    /// <summary>
    /// Measures reading the result value into an <c>in</c> helper.
    /// </summary>
    /// <returns>The extracted sum.</returns>
    [Benchmark(Description = "TryGetValue sink by in")]
    public int TryGetValueSinkByIn()
    {
        result.TryGetValue(out var currentPayload);
        return Helpers.ProjectByIn(in currentPayload);
    }

    private static class Helpers
    {
        public static Result<LargeStructBenchmarkPayload> CreateByValue(LargeStructBenchmarkPayload value)
        {
            return Result.Ok(value);
        }

        public static Result<LargeStructBenchmarkPayload> CreateByIn(in LargeStructBenchmarkPayload value)
        {
            return Result.Ok(value);
        }

        public static int ProjectByValue(LargeStructBenchmarkPayload value)
        {
            return value.Sum;
        }

        public static int ProjectByIn(in LargeStructBenchmarkPayload value)
        {
            return value.Sum;
        }
    }
}
