namespace SharedKernel.Results.Benchmarks;

/// <summary>
/// Larger value-type payload used to compare by-value and <c>in</c> helper calls in benchmarks.
/// </summary>
public readonly record struct LargeStructBenchmarkPayload(
    int Value1,
    int Value2,
    int Value3,
    int Value4,
    int Value5,
    int Value6,
    int Value7,
    int Value8)
{
    /// <summary>
    /// Gets the sum of all payload fields.
    /// </summary>
    public int Sum => Value1 + Value2 + Value3 + Value4 + Value5 + Value6 + Value7 + Value8;
}
