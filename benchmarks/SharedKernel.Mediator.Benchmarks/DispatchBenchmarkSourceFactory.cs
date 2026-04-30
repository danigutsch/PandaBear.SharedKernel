using System.Globalization;
using System.Text;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Creates synthetic dispatch benchmark sources for generated-style mediator comparisons.
/// </summary>
internal static class DispatchBenchmarkSourceFactory
{
    /// <summary>
    /// Creates benchmark source with the requested request volume.
    /// </summary>
    /// <param name="requestCount">The number of request/handler pairs to emit.</param>
    /// <returns>The synthetic dispatch benchmark source file.</returns>
    public static string CreateSource(int requestCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestCount);

        var targetIndex = requestCount - 1;
        var builder = new StringBuilder();
        builder.AppendLine("using SharedKernel.Mediator;");
        builder.AppendLine();
        builder.AppendLine("namespace BenchmarkApp;");
        builder.AppendLine();

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("public sealed record Request")
                .Append(index)
                .AppendLine("(int Id) : IQuery<int>;");
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
        }

        builder.AppendLine("public sealed class BenchmarkAppMediator : IMediator");
        builder.AppendLine("{");

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("    private readonly Request")
                .Append(index)
                .Append("Handler handler")
                .Append(index)
                .AppendLine(" = new();");
        }

        builder.AppendLine();

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("    public ValueTask<int> Send(Request")
                .Append(index)
                .AppendLine(" request, CancellationToken ct)");
            builder.AppendLine("    {");
            builder.Append("        return handler")
                .Append(index)
                .AppendLine(".Handle(request, ct);");
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
        builder.AppendLine("    private static ValueTask<object?> ThrowUnknownRequestObject(object request)");
        builder.AppendLine("    {");
        builder.AppendLine("        throw new NotSupportedException($\"Benchmark object dispatch is not available for request type '{request.GetType().FullName}'.\");");
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
}
