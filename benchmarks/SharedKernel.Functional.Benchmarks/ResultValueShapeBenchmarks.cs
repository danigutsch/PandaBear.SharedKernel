using BenchmarkDotNet.Attributes;

namespace SharedKernel.Functional.Benchmarks;

/// <summary>
/// Measures how payload shape affects common <see cref="Result{T}"/> success-path operations.
/// </summary>
[MemoryDiagnoser]
public class ResultValueShapeBenchmarks
{
    private const string ClassShape = "Class";
    private const string RecordClassShape = "RecordClass";
    private const string StructShape = "Struct";
    private const string ReadonlyRecordStructShape = "ReadonlyRecordStruct";
    private const string LargeStructShape = "LargeStruct";

    private object value = null!;
    private object result = null!;

    /// <summary>
    /// Gets or sets the payload shape under test.
    /// </summary>
    [Params(ClassShape, RecordClassShape, StructShape, ReadonlyRecordStructShape, LargeStructShape)]
    public string PayloadShape { get; set; } = ClassShape;

    /// <summary>
    /// Prepares the payload instance and its success result counterpart.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        switch (PayloadShape)
        {
            case ClassShape:
                value = new ClassPayload(42, 84);
                result = Result.Ok((ClassPayload)value);
                break;

            case RecordClassShape:
                value = new RecordClassPayload(42, 84);
                result = Result.Ok((RecordClassPayload)value);
                break;

            case StructShape:
                value = new StructPayload(42, 84);
                result = Result.Ok((StructPayload)value);
                break;

            case ReadonlyRecordStructShape:
                value = new ReadonlyRecordStructPayload(42, 84);
                result = Result.Ok((ReadonlyRecordStructPayload)value);
                break;

            case LargeStructShape:
                value = new LargeStructPayload(1, 2, 3, 4, 5, 6, 7, 8);
                result = Result.Ok((LargeStructPayload)value);
                break;

            default:
                throw new InvalidOperationException($"Unsupported payload shape '{PayloadShape}'.");
        }
    }

    /// <summary>
    /// Measures successful result creation for the configured payload shape.
    /// </summary>
    /// <returns>The created result.</returns>
    [Benchmark(Baseline = true, Description = "Result.Ok(T) by shape")]
    public object CreateSuccessResult()
    {
        return PayloadShape switch
        {
            ClassShape => Result.Ok((ClassPayload)value),
            RecordClassShape => Result.Ok((RecordClassPayload)value),
            StructShape => Result.Ok((StructPayload)value),
            ReadonlyRecordStructShape => Result.Ok((ReadonlyRecordStructPayload)value),
            LargeStructShape => Result.Ok((LargeStructPayload)value),
            _ => throw new InvalidOperationException($"Unsupported payload shape '{PayloadShape}'."),
        };
    }

    /// <summary>
    /// Measures mapping the configured payload shape to an integer.
    /// </summary>
    /// <returns>The mapped result.</returns>
    [Benchmark(Description = "Result.Map(T -> int) by shape")]
    public Result<int> MapSuccessResult()
    {
        return PayloadShape switch
        {
            ClassShape => ((Result<ClassPayload>)result).Map(static payload => payload.Sum),
            RecordClassShape => ((Result<RecordClassPayload>)result).Map(static payload => payload.Sum),
            StructShape => ((Result<StructPayload>)result).Map(static payload => payload.Sum),
            ReadonlyRecordStructShape => ((Result<ReadonlyRecordStructPayload>)result).Map(static payload => payload.Sum),
            LargeStructShape => ((Result<LargeStructPayload>)result).Map(static payload => payload.Sum),
            _ => throw new InvalidOperationException($"Unsupported payload shape '{PayloadShape}'."),
        };
    }

    /// <summary>
    /// Measures binding the configured payload shape to another success result.
    /// </summary>
    /// <returns>The bound result.</returns>
    [Benchmark(Description = "Result.Bind(T -> Result<int>) by shape")]
    public Result<int> BindSuccessResult()
    {
        return PayloadShape switch
        {
            ClassShape => ((Result<ClassPayload>)result).Bind(static payload => Result.Ok(payload.Sum)),
            RecordClassShape => ((Result<RecordClassPayload>)result).Bind(static payload => Result.Ok(payload.Sum)),
            StructShape => ((Result<StructPayload>)result).Bind(static payload => Result.Ok(payload.Sum)),
            ReadonlyRecordStructShape => ((Result<ReadonlyRecordStructPayload>)result).Bind(static payload => Result.Ok(payload.Sum)),
            LargeStructShape => ((Result<LargeStructPayload>)result).Bind(static payload => Result.Ok(payload.Sum)),
            _ => throw new InvalidOperationException($"Unsupported payload shape '{PayloadShape}'."),
        };
    }

    /// <summary>
    /// Measures reading the configured payload shape through <c>TryGetValue</c>.
    /// </summary>
    /// <returns>The extracted sum.</returns>
    [Benchmark(Description = "Result.TryGetValue() by shape")]
    public int TryGetValueFromSuccessResult()
    {
        return PayloadShape switch
        {
            ClassShape => TryGetValue((Result<ClassPayload>)result),
            RecordClassShape => TryGetValue((Result<RecordClassPayload>)result),
            StructShape => TryGetValue((Result<StructPayload>)result),
            ReadonlyRecordStructShape => TryGetValue((Result<ReadonlyRecordStructPayload>)result),
            LargeStructShape => TryGetValue((Result<LargeStructPayload>)result),
            _ => throw new InvalidOperationException($"Unsupported payload shape '{PayloadShape}'."),
        };
    }

    private static int TryGetValue(Result<ClassPayload> source)
    {
        source.TryGetValue(out var payload);
        ArgumentNullException.ThrowIfNull(payload);
        return payload.Sum;
    }

    private static int TryGetValue(Result<RecordClassPayload> source)
    {
        source.TryGetValue(out var payload);
        ArgumentNullException.ThrowIfNull(payload);
        return payload.Sum;
    }

    private static int TryGetValue(Result<StructPayload> source)
    {
        source.TryGetValue(out var payload);
        return payload.Sum;
    }

    private static int TryGetValue(Result<ReadonlyRecordStructPayload> source)
    {
        source.TryGetValue(out var payload);
        return payload.Sum;
    }

    private static int TryGetValue(Result<LargeStructPayload> source)
    {
        source.TryGetValue(out var payload);
        return payload.Sum;
    }

    private sealed class ClassPayload(int left, int right)
    {
        public int Left { get; } = left;

        public int Right { get; } = right;

        public int Sum => Left + Right;
    }

    private sealed record RecordClassPayload(int Left, int Right)
    {
        public int Sum => Left + Right;
    }

    private readonly struct StructPayload(int left, int right)
    {
        public int Left { get; } = left;

        public int Right { get; } = right;

        public int Sum => Left + Right;
    }

    private readonly record struct ReadonlyRecordStructPayload(int Left, int Right)
    {
        public int Sum => Left + Right;
    }

    private readonly struct LargeStructPayload(
        int value1,
        int value2,
        int value3,
        int value4,
        int value5,
        int value6,
        int value7,
        int value8)
    {
        public int Value1 { get; } = value1;

        public int Value2 { get; } = value2;

        public int Value3 { get; } = value3;

        public int Value4 { get; } = value4;

        public int Value5 { get; } = value5;

        public int Value6 { get; } = value6;

        public int Value7 { get; } = value7;

        public int Value8 { get; } = value8;

        public int Sum => Value1 + Value2 + Value3 + Value4 + Value5 + Value6 + Value7 + Value8;
    }
}
