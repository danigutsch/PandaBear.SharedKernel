using BenchmarkDotNet.Attributes;

namespace SharedKernel.Functional.Benchmarks;

/// <summary>
/// Measures structured error conversion and aggregation flows.
/// </summary>
[MemoryDiagnoser]
public class ResultFailureFlowBenchmarks
{
    private static readonly Result InvalidResult = Result.Invalid(
        "Validation failed.",
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tourCode"] = ["Tour code is required."],
            ["passengerName"] = ["Passenger name is required."],
        });

    private static readonly Result<int> GenericInvalidResult = Result.Invalid<int>(
        "Validation failed.",
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tourCode"] = ["Tour code is required."],
            ["passengerName"] = ["Passenger name is required."],
        });

    /// <summary>
    /// Measures failed result conversion from non-generic to generic form.
    /// </summary>
    /// <returns>The converted result.</returns>
    [Benchmark(Baseline = true, Description = "Convert invalid to generic")]
    public Result<int> ConvertInvalidToGeneric()
    {
        return InvalidResult.ConvertError<int>();
    }

    /// <summary>
    /// Measures failed result conversion from generic to non-generic form.
    /// </summary>
    /// <returns>The converted result.</returns>
    [Benchmark(Description = "Convert invalid to non-generic")]
    public Result ConvertInvalidToNonGeneric()
    {
        return GenericInvalidResult.ConvertError();
    }

    /// <summary>
    /// Measures aggregation of multiple invalid results into one structured failure.
    /// </summary>
    /// <returns>The aggregated result.</returns>
    [Benchmark(Description = "Aggregate validation errors")]
    public Result AggregateValidationErrors()
    {
        var errors = new ValidationErrors();
        errors.Add(Result.Invalid("Validation failed.", "tourCode", "Tour code is required."));
        errors.Add(Result.Invalid("Validation failed.", "passengerName", "Passenger name is required."));
        return errors.ToResult();
    }
}
