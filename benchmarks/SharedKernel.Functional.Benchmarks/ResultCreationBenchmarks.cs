using BenchmarkDotNet.Attributes;

namespace SharedKernel.Functional.Benchmarks;

/// <summary>
/// Measures core SharedKernel.Functional creation paths.
/// </summary>
[MemoryDiagnoser]
public class ResultCreationBenchmarks
{
    private const string ErrorDetail = "Unexpected failure.";
    private const string ValidationDetail = "Validation failed.";
    private static readonly Dictionary<string, string[]> ValidationErrors = new(StringComparer.Ordinal)
    {
        ["tourCode"] = ["Tour code is required."],
        ["passengerName"] = ["Passenger name is required."],
    };

    /// <summary>
    /// Measures non-generic success creation.
    /// </summary>
    /// <returns>The created result.</returns>
    [Benchmark(Baseline = true, Description = "Result.Ok()")]
    public Result NonGenericSuccess()
    {
        return Result.Ok();
    }

    /// <summary>
    /// Measures generic success creation.
    /// </summary>
    /// <returns>The created result.</returns>
    [Benchmark(Description = "Result.Ok(T)")]
    public Result<int> GenericSuccess()
    {
        return Result.Ok(42);
    }

    /// <summary>
    /// Measures non-generic failure creation.
    /// </summary>
    /// <returns>The created result.</returns>
    [Benchmark(Description = "Result.Error()")]
    public Result NonGenericFailure()
    {
        return Result.Error(ErrorDetail);
    }

    /// <summary>
    /// Measures generic failure creation.
    /// </summary>
    /// <returns>The created result.</returns>
    [Benchmark(Description = "Result.Error(T)")]
    public Result<int> GenericFailure()
    {
        return Result.Error<int>(ErrorDetail);
    }

    /// <summary>
    /// Measures single-field validation failure creation.
    /// </summary>
    /// <returns>The created result.</returns>
    [Benchmark(Description = "Result.Invalid(field)")]
    public Result SingleFieldValidationFailure()
    {
        return Result.Invalid(ValidationDetail, "tourCode", "Tour code is required.");
    }

    /// <summary>
    /// Measures multi-field validation failure creation.
    /// </summary>
    /// <returns>The created result.</returns>
    [Benchmark(Description = "Result.Invalid(dictionary)")]
    public Result MultiFieldValidationFailure()
    {
        return Result.Invalid(ValidationDetail, ValidationErrors);
    }
}
