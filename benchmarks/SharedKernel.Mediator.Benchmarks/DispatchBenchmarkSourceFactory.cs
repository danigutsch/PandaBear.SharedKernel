using System.Globalization;
using System.Text;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Creates synthetic dispatch benchmark sources for generated-style mediator comparisons.
/// </summary>
internal static class DispatchBenchmarkSourceFactory
{
    public const string ClassShape = "Class";
    public const string RecordClassShape = "RecordClass";
    public const string ReadonlyRecordStructShape = "ReadonlyRecordStruct";
    public const int NoPipelineCount = 0;
    public const int OnePipelineCount = 1;
    public const int ThreePipelineCount = 3;
    public const int TenPipelineCount = 10;

    /// <summary>
    /// Creates benchmark source with the requested request volume.
    /// </summary>
    /// <param name="requestCount">The number of request/handler pairs to emit.</param>
    /// <param name="requestShape">The request shape to emit.</param>
    /// <param name="pipelineCount">The number of pipeline behaviors to emit per request.</param>
    /// <returns>The synthetic dispatch benchmark source file.</returns>
    public static string CreateSource(int requestCount, string requestShape, int pipelineCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestCount);
        ArgumentOutOfRangeException.ThrowIfNegative(pipelineCount);

        var targetIndex = requestCount - 1;
        var builder = new StringBuilder();
        builder.AppendLine("using SharedKernel.Mediator;");
        builder.AppendLine("using System.Collections.Frozen;");
        builder.AppendLine();
        builder.AppendLine("namespace BenchmarkApp;");
        builder.AppendLine();

        for (var index = 0; index < requestCount; index++)
        {
            AppendRequestDeclaration(builder, index, requestShape);
            builder.AppendLine();
            builder.Append("public sealed class Request")
                .Append(index)
                .Append("Handler : IQueryHandler<Request")
                .Append(index)
                .AppendLine(", int>");
            builder.AppendLine("{");
            builder.Append("    public ValueTask<int> Handle(Request")
                .Append(index)
                .Append(" request, CancellationToken ct) => ValueTask.FromResult(request.Id + ")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
            builder.AppendLine("}");
            builder.AppendLine();

            for (var pipelineIndex = 0; pipelineIndex < pipelineCount; pipelineIndex++)
            {
                builder.Append("[PipelineOrder(PipelineStage.Validation, Order = ")
                    .Append(pipelineIndex)
                    .AppendLine(")]");
                builder.Append("public sealed class Request")
                    .Append(index)
                    .Append("Pipeline")
                    .Append(pipelineIndex)
                    .Append(" : IPipelineBehavior<Request")
                    .Append(index)
                    .AppendLine(", int>");
                builder.AppendLine("{");
                builder.Append("    public ValueTask<int> Handle(Request")
                    .Append(index)
                    .Append(" request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();")
                    .AppendLine();
                builder.AppendLine("}");
                builder.AppendLine();
            }
        }

        builder.AppendLine("public sealed class BenchmarkAppMediator : IMediator");
        builder.AppendLine("{");
        builder.AppendLine("    private static readonly Dictionary<Type, Func<BenchmarkAppMediator, IRequest<int>, CancellationToken, ValueTask<int>>> DictionaryDispatchers = CreateDictionaryDispatchers();");
        builder.AppendLine("    private static readonly FrozenDictionary<Type, Func<BenchmarkAppMediator, IRequest<int>, CancellationToken, ValueTask<int>>> FrozenDictionaryDispatchers = CreateFrozenDictionaryDispatchers();");
        builder.AppendLine();

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("    private readonly Request")
                .Append(index)
                .Append("Handler handler")
                .Append(index)
                .AppendLine(" = new();");

            for (var pipelineIndex = 0; pipelineIndex < pipelineCount; pipelineIndex++)
            {
                builder.Append("    private readonly Request")
                    .Append(index)
                    .Append("Pipeline")
                    .Append(pipelineIndex)
                    .Append(" pipeline")
                    .Append(index)
                    .Append('_')
                    .Append(pipelineIndex)
                    .AppendLine(" = new();");
            }
        }

        builder.AppendLine();

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("    public ValueTask<int> Send(Request")
                .Append(index)
                .AppendLine(" request, CancellationToken ct)");
            builder.AppendLine("    {");

            if (pipelineCount == 0)
            {
                builder.Append("        return handler")
                    .Append(index)
                    .AppendLine(".Handle(request, ct);");
            }
            else
            {
                builder.AppendLine("        RequestHandlerContinuation<int> next = () =>");
                builder.Append("            handler")
                    .Append(index)
                    .AppendLine(".Handle(request, ct);");
                builder.AppendLine();

                for (var pipelineIndex = pipelineCount - 1; pipelineIndex >= 0; pipelineIndex--)
                {
                    builder.Append("        var pipeline")
                        .Append(pipelineIndex)
                        .AppendLine("Next = next;");
                    builder.Append("        next = () => pipeline")
                        .Append(index)
                        .Append('_')
                        .Append(pipelineIndex)
                        .Append(".Handle(request, pipeline")
                        .Append(pipelineIndex)
                        .AppendLine("Next, ct);");
                    builder.AppendLine();
                }

                builder.AppendLine("        return next();");
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        builder.AppendLine("    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        ArgumentNullException.ThrowIfNull(request);");
        builder.AppendLine();
        builder.AppendLine("        return request switch");
        builder.AppendLine("        {");

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("            Request")
                .Append(index)
                .Append(" typed => Cast<int, TResponse>(Send(typed, ct)),")
                .AppendLine();
        }

        builder.AppendLine("            _ => ThrowNoHandler(request),");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public ValueTask<int> SendViaStaticGenericCache<TRequest>(TRequest request, CancellationToken ct)");
        builder.AppendLine("        where TRequest : IRequest<int>");
        builder.AppendLine("    {");
        builder.AppendLine("        ArgumentNullException.ThrowIfNull(request);");
        builder.AppendLine("        return GenericDispatchCache<TRequest>.Dispatch(this, request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public ValueTask<int> SendViaDictionary(IRequest<int> request, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        ArgumentNullException.ThrowIfNull(request);");
        builder.AppendLine("        return DictionaryDispatchers.TryGetValue(request.GetType(), out var dispatch)");
        builder.AppendLine("            ? dispatch(this, request, ct)");
        builder.AppendLine("            : ThrowNoHandler(request);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public ValueTask<int> SendViaFrozenDictionary(IRequest<int> request, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        ArgumentNullException.ThrowIfNull(request);");
        builder.AppendLine("        return FrozenDictionaryDispatchers.TryGetValue(request.GetType(), out var dispatch)");
        builder.AppendLine("            ? dispatch(this, request, ct)");
        builder.AppendLine("            : ThrowNoHandler(request);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public ValueTask<object?> SendObject(object request, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        ArgumentNullException.ThrowIfNull(request);");
        builder.AppendLine();
        builder.AppendLine("        return request switch");
        builder.AppendLine("        {");

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("            Request")
                .Append(index)
                .Append(" typed => Box(Send(typed, ct)),")
                .AppendLine();
        }

        builder.AppendLine("            _ => ThrowUnknownRequestObject(request),");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken ct)");
        builder.AppendLine("        where TNotification : INotification");
        builder.AppendLine("    {");
        builder.AppendLine("        ArgumentNullException.ThrowIfNull(notification);");
        builder.AppendLine("        throw new NotSupportedException(\"Benchmark notification dispatch is not implemented.\");");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public IAsyncEnumerable<TResponse> Send<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        ArgumentNullException.ThrowIfNull(request);");
        builder.AppendLine("        throw new NotSupportedException(\"Benchmark stream dispatch is not implemented.\");");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static async ValueTask<TTarget> Cast<TSource, TTarget>(ValueTask<TSource> source)");
        builder.AppendLine("    {");
        builder.AppendLine("        var result = await source.ConfigureAwait(false);");
        builder.AppendLine("        if (result is TTarget typed)");
        builder.AppendLine("        {");
        builder.AppendLine("            return typed;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        throw new InvalidOperationException($\"Benchmark mediator returned '{typeof(TSource).FullName}' when '{typeof(TTarget).FullName}' was expected.\");");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static async ValueTask<object?> Box(ValueTask<int> source)");
        builder.AppendLine("    {");
        builder.AppendLine("        return await source.ConfigureAwait(false);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static ValueTask<TResponse> ThrowNoHandler<TResponse>(IRequest<TResponse> request)");
        builder.AppendLine("    {");
        builder.AppendLine("        throw new NotSupportedException($\"Benchmark mediator dispatch is not available for request type '{request.GetType().FullName}'.\");");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static Dictionary<Type, Func<BenchmarkAppMediator, IRequest<int>, CancellationToken, ValueTask<int>>> CreateDictionaryDispatchers()");
        builder.AppendLine("    {");
        builder.AppendLine("        return new Dictionary<Type, Func<BenchmarkAppMediator, IRequest<int>, CancellationToken, ValueTask<int>>>");
        builder.AppendLine("        {");

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("            [typeof(Request")
                .Append(index)
                .Append(")] = static (mediator, request, ct) => mediator.Send((Request")
                .Append(index)
                .AppendLine(")request, ct),");
        }

        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static FrozenDictionary<Type, Func<BenchmarkAppMediator, IRequest<int>, CancellationToken, ValueTask<int>>> CreateFrozenDictionaryDispatchers()");
        builder.AppendLine("    {");
        builder.AppendLine("        return CreateDictionaryDispatchers().ToFrozenDictionary();");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static ValueTask<object?> ThrowUnknownRequestObject(object request)");
        builder.AppendLine("    {");
        builder.AppendLine("        throw new NotSupportedException($\"Benchmark object dispatch is not available for request type '{request.GetType().FullName}'.\");");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("file static class GenericDispatchCache<TRequest>");
        builder.AppendLine("    where TRequest : IRequest<int>");
        builder.AppendLine("{");
        builder.AppendLine("    public static Func<BenchmarkAppMediator, TRequest, CancellationToken, ValueTask<int>> Dispatch { get; } = CreateDispatch();");
        builder.AppendLine();
        builder.AppendLine("    private static Func<BenchmarkAppMediator, TRequest, CancellationToken, ValueTask<int>> CreateDispatch()");
        builder.AppendLine("    {");

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("        if (typeof(TRequest) == typeof(Request")
                .Append(index)
                .AppendLine("))");
            builder.AppendLine("        {");
            builder.Append("            return static (mediator, request, ct) => mediator.Send((Request")
                .Append(index)
                .AppendLine(")(object)request, ct);");
            builder.AppendLine("        }");
            builder.AppendLine();
        }

        builder.AppendLine("        return static (_, request, _) => ThrowNoHandler(request);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static ValueTask<int> ThrowNoHandler(IRequest<int> request)");
        builder.AppendLine("    {");
        builder.AppendLine("        throw new NotSupportedException($\"Benchmark cached dispatch is not available for request type '{request.GetType().FullName}'.\");");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("public static class BenchmarkExports");
        builder.AppendLine("{");
        builder.Append("    public static Func<CancellationToken, ValueTask<int>> CreateDirectHandlerCall() => ct => new Request")
            .Append(targetIndex)
            .Append("Handler().Handle(new Request")
            .Append(targetIndex)
            .AppendLine("(42), ct);");
        builder.Append("    public static Func<CancellationToken, ValueTask<int>> CreateGeneratedTypedOverload()");
        builder.AppendLine();
        builder.AppendLine("    {");
        builder.AppendLine("        var mediator = new BenchmarkAppMediator();");
        builder.Append("        var request = new Request")
            .Append(targetIndex)
            .AppendLine("(42);");
        builder.AppendLine("        return ct => mediator.Send(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    public static Func<CancellationToken, ValueTask<int>> CreateGeneratedGenericSwitch()");
        builder.AppendLine();
        builder.AppendLine("    {");
        builder.AppendLine("        var mediator = new BenchmarkAppMediator();");
        builder.Append("        IRequest<int> request = new Request")
            .Append(targetIndex)
            .AppendLine("(42);");
        builder.AppendLine("        return ct => mediator.Send(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    public static Func<CancellationToken, ValueTask<int>> CreateStaticGenericCache()");
        builder.AppendLine();
        builder.AppendLine("    {");
        builder.AppendLine("        var mediator = new BenchmarkAppMediator();");
        builder.Append("        var request = new Request")
            .Append(targetIndex)
            .AppendLine("(42);");
        builder.AppendLine("        return ct => mediator.SendViaStaticGenericCache(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    public static Func<CancellationToken, ValueTask<int>> CreateDictionaryDispatch()");
        builder.AppendLine();
        builder.AppendLine("    {");
        builder.AppendLine("        var mediator = new BenchmarkAppMediator();");
        builder.Append("        IRequest<int> request = new Request")
            .Append(targetIndex)
            .AppendLine("(42);");
        builder.AppendLine("        return ct => mediator.SendViaDictionary(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    public static Func<CancellationToken, ValueTask<int>> CreateFrozenDictionaryDispatch()");
        builder.AppendLine();
        builder.AppendLine("    {");
        builder.AppendLine("        var mediator = new BenchmarkAppMediator();");
        builder.Append("        IRequest<int> request = new Request")
            .Append(targetIndex)
            .AppendLine("(42);");
        builder.AppendLine("        return ct => mediator.SendViaFrozenDictionary(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    public static Func<CancellationToken, ValueTask<object?>> CreateGeneratedObjectSwitch()");
        builder.AppendLine();
        builder.AppendLine("    {");
        builder.AppendLine("        var mediator = new BenchmarkAppMediator();");
        builder.Append("        object request = new Request")
            .Append(targetIndex)
            .AppendLine("(42);");
        builder.AppendLine("        return ct => mediator.SendObject(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendRequestDeclaration(StringBuilder builder, int index, string requestShape)
    {
        switch (requestShape)
        {
            case ClassShape:
                builder.Append("public sealed class Request")
                    .Append(index)
                    .AppendLine("(int id) : IQuery<int>")
                    .AppendLine("{")
                    .AppendLine("    public int Id { get; } = id;")
                    .AppendLine("}");
                break;

            case RecordClassShape:
                builder.Append("public sealed record Request")
                    .Append(index)
                    .AppendLine("(int Id) : IQuery<int>;");
                break;

            case ReadonlyRecordStructShape:
                builder.Append("public readonly record struct Request")
                    .Append(index)
                    .AppendLine("(int Id) : IQuery<int>;");
                break;

            default:
                throw new InvalidOperationException($"Unsupported request shape '{requestShape}'.");
        }
    }
}
