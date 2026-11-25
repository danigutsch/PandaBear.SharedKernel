using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace SharedKernel.Results.Benchmarks;

/// <summary>Compares constructing and reading the library outcomes with C# union outcomes.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class UnionOutcomeBenchmarks
{
    private const int BatchSize = 256;

    // Reuse the same error to measure the carrier rather than error construction.
    private static readonly ResultError Missing = Result.NotFound("Missing record").ErrorDetails!;

    /// <summary>Checks that the measured pairs return the same decisions.</summary>
    /// <exception cref="InvalidOperationException">Thrown when results differ.</exception>
    [GlobalSetup]
    public void ValidateComparisons()
    {
        if (ExistingResult() != DeclaredUnionResult() || ExistingOption() != DeclaredUnionOption())
        {
            throw new InvalidOperationException("The compared implementations must produce the same decisions.");
        }
    }

    /// <summary>Measures the existing result through successful and failed calls.</summary>
    /// <returns>The sum of the matched decisions.</returns>
    [Benchmark(Baseline = true, OperationsPerInvoke = BatchSize)]
    [BenchmarkCategory("Result")]
    public int ExistingResult()
    {
        var sum = 0;
        for (var index = 0; index < BatchSize; index++)
        {
            var outcome = CreateResult(index);
            sum += outcome.Match(static value => value, static error => error.Code.Length);
        }

        return sum;
    }

    /// <summary>Measures a union carrying the same result information.</summary>
    /// <returns>The sum of the matched decisions.</returns>
    [Benchmark(OperationsPerInvoke = BatchSize)]
    [BenchmarkCategory("Result")]
    public int DeclaredUnionResult()
    {
        var sum = 0;
        for (var index = 0; index < BatchSize; index++)
        {
            var outcome = CreateUnionResult(index);
            sum += outcome switch
            {
                Success success => success.Value,
                Failure failure => failure.Error.Code.Length,
                null => throw new InvalidOperationException("Uninitialized result."),
            };
        }

        return sum;
    }

    /// <summary>Measures the existing option through present and missing values.</summary>
    /// <returns>The sum of the matched decisions.</returns>
    [Benchmark(Baseline = true, OperationsPerInvoke = BatchSize)]
    [BenchmarkCategory("Option")]
    public int ExistingOption()
    {
        var sum = 0;
        for (var index = 0; index < BatchSize; index++)
        {
            var outcome = CreateOption(index);
            sum += outcome.Match(static value => value, static () => 0);
        }

        return sum;
    }

    /// <summary>Measures a union storing presence as a value-type case and absence as null.</summary>
    /// <returns>The sum of the matched decisions.</returns>
    [Benchmark(OperationsPerInvoke = BatchSize)]
    [BenchmarkCategory("Option")]
    public int DeclaredUnionOption()
    {
        var sum = 0;
        for (var index = 0; index < BatchSize; index++)
        {
            var outcome = CreateUnionOption(index);
            sum += outcome switch
            {
                int value => value,
                null => 0,
            };
        }

        return sum;
    }

    // Preserve the producer/caller boundary so the JIT cannot erase storage costs by
    // combining construction with the match.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Result<int> CreateResult(int index) =>
        IsPresent(index) ? Result.Created(index) : Result<int>.FromError(Missing);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ResultUnion CreateUnionResult(int index) =>
        IsPresent(index) ? new Success(ResultStatus.Created, index) : new Failure(ResultStatus.NotFound, Missing);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Option<int> CreateOption(int index) =>
        IsPresent(index) ? Option.Some(index) : Option.None<int>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OptionUnion CreateUnionOption(int index) =>
        IsPresent(index) ? new OptionUnion(index) : default;

    private static bool IsPresent(int index) => (index & 3) != 0;

    private readonly record struct Success(ResultStatus Status, int Value);

    private readonly record struct Failure(ResultStatus Status, ResultError Error);

    private union ResultUnion(Success, Failure);

    private union OptionUnion(int?);
}
