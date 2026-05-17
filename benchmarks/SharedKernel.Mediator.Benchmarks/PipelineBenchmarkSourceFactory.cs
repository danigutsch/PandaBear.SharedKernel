using System.Globalization;
using System.Text;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Creates synthetic benchmark sources for pipeline-dispatch strategy comparisons.
/// </summary>
internal static class PipelineBenchmarkSourceFactory
{
    public const string ClassShape = "Class";
    public const string StructShape = "Struct";
    public const string SynchronousCompletion = "Synchronous";
    public const string AsynchronousCompletion = "Asynchronous";

    public static string CreateSource(string requestShape, int pipelineDepth, string completionMode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pipelineDepth);

        var builder = new StringBuilder();
        builder.AppendLine("using SharedKernel.Mediator;");
        builder.AppendLine();
        builder.AppendLine("namespace BenchmarkApp;");
        builder.AppendLine();

        AppendRequestDeclaration(builder, requestShape);
        builder.AppendLine();
        AppendHandlerDeclaration(builder, completionMode);
        builder.AppendLine();
        AppendInstancePipelines(builder, pipelineDepth);
        AppendStaticPipelineInterface(builder, pipelineDepth);
        AppendNoPipelineDispatcher(builder, requestShape);
        AppendDelegateChainDispatcher(builder, requestShape, pipelineDepth);
        AppendStaticDelegateChainDispatcher(builder, requestShape, pipelineDepth);
        AppendGeneratedNestedDispatcher(builder, requestShape, pipelineDepth);
        AppendChainObjectDispatcher(builder, requestShape, pipelineDepth);
        AppendStaticGenericDispatcher(builder, requestShape, pipelineDepth);
        AppendExports(builder);

        return builder.ToString();
    }

    private static void AppendRequestDeclaration(StringBuilder builder, string requestShape)
    {
        switch (requestShape)
        {
            case ClassShape:
                builder.AppendLine("public sealed class BenchmarkRequest(int id) : IQuery<int>");
                builder.AppendLine("{");
                builder.AppendLine("    public int Id { get; } = id;");
                builder.AppendLine("}");
                break;

            case StructShape:
                builder.AppendLine("public readonly record struct BenchmarkRequest(int Id) : IQuery<int>;");
                break;

            default:
                throw new InvalidOperationException($"Unsupported request shape '{requestShape}'.");
        }
    }

    private static void AppendHandlerDeclaration(StringBuilder builder, string completionMode)
    {
        const string requestIdAccessor = "request.Id";

        builder.AppendLine("public sealed class BenchmarkRequestHandler : IQueryHandler<BenchmarkRequest, int>");
        builder.AppendLine("{");
        builder.AppendLine("    public ValueTask<int> Handle(BenchmarkRequest request, CancellationToken ct)");
        builder.AppendLine("    {");

        switch (completionMode)
        {
            case SynchronousCompletion:
                builder.AppendLine("        ct.ThrowIfCancellationRequested();");
                builder.Append("        return ValueTask.FromResult(")
                    .Append(requestIdAccessor)
                    .AppendLine(");");
                break;

            case AsynchronousCompletion:
                builder.AppendLine("        return HandleAsync(request, ct);");
                break;

            default:
                throw new InvalidOperationException($"Unsupported completion mode '{completionMode}'.");
        }

        builder.AppendLine("    }");

        if (completionMode == AsynchronousCompletion)
        {
            builder.AppendLine();
            builder.AppendLine("    private static async ValueTask<int> HandleAsync(BenchmarkRequest request, CancellationToken ct)");
            builder.AppendLine("    {");
            builder.AppendLine("        await Task.Yield();");
            builder.AppendLine("        ct.ThrowIfCancellationRequested();");
            builder.Append("        return ")
                .Append(requestIdAccessor)
                .AppendLine(";");
            builder.AppendLine("    }");
        }

        builder.AppendLine("}");
    }

    private static void AppendInstancePipelines(StringBuilder builder, int pipelineDepth)
    {
        for (var index = 0; index < pipelineDepth; index++)
        {
            builder.Append("[PipelineOrder(PipelineStage.Validation, Order = ")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(")]");
            builder.Append("public sealed class InstancePipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(" : IPipelineBehavior<BenchmarkRequest, int>")
                .AppendLine();
            builder.AppendLine("{");
            builder.AppendLine("    public ValueTask<int> Handle(BenchmarkRequest request, RequestHandlerContinuation<int> next, CancellationToken ct)");
            builder.AppendLine("    {");
            builder.AppendLine("        ct.ThrowIfCancellationRequested();");
            builder.AppendLine("        return next();");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
        }
    }

    private static void AppendStaticPipelineInterface(StringBuilder builder, int pipelineDepth)
    {
        builder.AppendLine("public interface IStaticPipeline<TRequest>");
        builder.AppendLine("    where TRequest : IRequest<int>");
        builder.AppendLine("{");
        builder.AppendLine("    static abstract ValueTask<int> Handle(TRequest request, RequestHandlerContinuation<int> next, CancellationToken ct);");
        builder.AppendLine("}");
        builder.AppendLine();

        for (var index = 0; index < pipelineDepth; index++)
        {
            builder.Append("public sealed class StaticPipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(" : IStaticPipeline<BenchmarkRequest>")
                .AppendLine();
            builder.AppendLine("{");
            builder.AppendLine("    public static ValueTask<int> Handle(BenchmarkRequest request, RequestHandlerContinuation<int> next, CancellationToken ct)");
            builder.AppendLine("    {");
            builder.AppendLine("        ct.ThrowIfCancellationRequested();");
            builder.AppendLine("        return next();");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
        }
    }

    private static void AppendNoPipelineDispatcher(StringBuilder builder, string requestShape)
    {
        builder.AppendLine("public static class NoPipelineDispatcher");
        builder.AppendLine("{");
        builder.AppendLine("    private static readonly BenchmarkRequestHandler Handler = new();");
        builder.AppendLine();
        builder.AppendLine("    public static ValueTask<int> Send(BenchmarkRequest request, CancellationToken ct)");
        builder.AppendLine("    {");
        AppendNullableGuard(builder, requestShape, "request", "        ");
        builder.AppendLine("        return Handler.Handle(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendDelegateChainDispatcher(StringBuilder builder, string requestShape, int pipelineDepth)
    {
        builder.AppendLine("public sealed class DelegateChainDispatcher");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly BenchmarkRequestHandler handler = new();");

        for (var index = 0; index < pipelineDepth; index++)
        {
            builder.Append("    private readonly InstancePipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(" pipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" = new();");
        }

        if (pipelineDepth > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine("    public ValueTask<int> Send(BenchmarkRequest request, CancellationToken ct)");
        builder.AppendLine("    {");
        AppendNullableGuard(builder, requestShape, "request", "        ");
        builder.AppendLine("        RequestHandlerContinuation<int> next = () => handler.Handle(request, ct);");

        if (pipelineDepth > 0)
        {
            builder.AppendLine();
        }

        for (var index = pipelineDepth - 1; index >= 0; index--)
        {
            builder.Append("        var continuation")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" = next;");
            builder.Append("        next = () => pipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(".Handle(request, continuation")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(", ct);");

            if (index > 0)
            {
                builder.AppendLine();
            }
        }

        builder.AppendLine("        return next();");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendStaticDelegateChainDispatcher(StringBuilder builder, string requestShape, int pipelineDepth)
    {
        builder.AppendLine("public sealed class StaticDelegateChainDispatcher");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly BenchmarkRequestHandler handler = new();");

        for (var index = 0; index < pipelineDepth; index++)
        {
            builder.Append("    private readonly InstancePipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(" pipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" = new();");
        }

        builder.AppendLine();
        builder.AppendLine("    private readonly Func<BenchmarkRequest, CancellationToken, ValueTask<int>> pipeline;");
        builder.AppendLine();
        builder.AppendLine("    public StaticDelegateChainDispatcher()");
        builder.AppendLine("    {");
        builder.AppendLine("        pipeline = CreatePipeline();");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public ValueTask<int> Send(BenchmarkRequest request, CancellationToken ct)");
        builder.AppendLine("    {");
        AppendNullableGuard(builder, requestShape, "request", "        ");
        builder.AppendLine("        return pipeline(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private Func<BenchmarkRequest, CancellationToken, ValueTask<int>> CreatePipeline()");
        builder.AppendLine("    {");
        builder.AppendLine("        Func<BenchmarkRequest, CancellationToken, ValueTask<int>> next = (request, ct) => handler.Handle(request, ct);");

        if (pipelineDepth > 0)
        {
            builder.AppendLine();
        }

        for (var index = pipelineDepth - 1; index >= 0; index--)
        {
            builder.Append("        var continuation")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" = next;");
            builder.Append("        next = (request, ct) => pipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(".Handle(request, () => continuation")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("(request, ct), ct);")
                .AppendLine();

            if (index > 0)
            {
                builder.AppendLine();
            }
        }

        builder.AppendLine("        return next;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendGeneratedNestedDispatcher(StringBuilder builder, string requestShape, int pipelineDepth)
    {
        builder.AppendLine("public sealed class GeneratedNestedDispatcher");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly BenchmarkRequestHandler handler = new();");

        for (var index = 0; index < pipelineDepth; index++)
        {
            builder.Append("    private readonly InstancePipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(" pipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" = new();");
        }

        if (pipelineDepth > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine("    public ValueTask<int> Send(BenchmarkRequest request, CancellationToken ct)");
        builder.AppendLine("    {");
        AppendNullableGuard(builder, requestShape, "request", "        ");
        builder.Append("        return ")
            .Append(BuildNestedInvocation(pipelineDepth))
            .AppendLine(";");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendChainObjectDispatcher(StringBuilder builder, string requestShape, int pipelineDepth)
    {
        builder.AppendLine("public sealed class ChainObjectDispatcher");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly BenchmarkRequestHandler handler = new();");
        builder.AppendLine("    private readonly IPipelineBehavior<BenchmarkRequest, int>[] pipelines =");
        builder.AppendLine("    [");

        for (var index = 0; index < pipelineDepth; index++)
        {
            builder.Append("        new InstancePipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine("(),");
        }

        builder.AppendLine("    ];");
        builder.AppendLine();
        builder.AppendLine("    public ValueTask<int> Send(BenchmarkRequest request, CancellationToken ct)");
        builder.AppendLine("    {");
        AppendNullableGuard(builder, requestShape, "request", "        ");
        builder.AppendLine("        return new PipelineChain(handler, pipelines, request, ct).Invoke();");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private sealed class PipelineChain(");
        builder.AppendLine("        BenchmarkRequestHandler handler,");
        builder.AppendLine("        IPipelineBehavior<BenchmarkRequest, int>[] pipelines,");
        builder.AppendLine("        BenchmarkRequest request,");
        builder.AppendLine("        CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        private int index;");
        builder.AppendLine();
        builder.AppendLine("        public ValueTask<int> Invoke()");
        builder.AppendLine("        {");
        builder.AppendLine("            if (index >= pipelines.Length)");
        builder.AppendLine("            {");
        builder.AppendLine("                return handler.Handle(request, ct);");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            var current = pipelines[index];");
        builder.AppendLine("            index++;");
        builder.AppendLine("            return current.Handle(request, Invoke, ct);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendStaticGenericDispatcher(StringBuilder builder, string requestShape, int pipelineDepth)
    {
        builder.AppendLine("public static class StaticGenericHandler<TRequest>");
        builder.AppendLine("    where TRequest : IRequest<int>");
        builder.AppendLine("{");
        builder.AppendLine("    private static readonly BenchmarkRequestHandler Handler = new();");
        builder.AppendLine();
        builder.AppendLine("    public static ValueTask<int> Handle(TRequest request, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (request is BenchmarkRequest typed)");
        builder.AppendLine("        {");
        builder.AppendLine("            return Handler.Handle(typed, ct);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        throw new NotSupportedException($\"Static generic benchmark pipeline does not support request type '{typeof(TRequest).FullName}'.\");");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("public static class StaticGenericPipelineDispatcher");
        builder.AppendLine("{");
        builder.AppendLine("    public static ValueTask<int> Send(BenchmarkRequest request, CancellationToken ct)");
        builder.AppendLine("    {");
        AppendNullableGuard(builder, requestShape, "request", "        ");

        if (pipelineDepth == 0)
        {
            builder.AppendLine("        return StaticGenericHandler<BenchmarkRequest>.Handle(request, ct);");
        }
        else
        {
            builder.Append("        return StaticGenericPipeline<BenchmarkRequest");

            for (var index = 0; index < pipelineDepth; index++)
            {
                builder.Append(", StaticPipeline")
                    .Append(index.ToString(CultureInfo.InvariantCulture));
            }

            builder.AppendLine(">.Invoke(request, ct);");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();

        if (pipelineDepth == 0)
        {
            return;
        }

        builder.Append("public static class StaticGenericPipeline<TRequest");

        for (var index = 0; index < pipelineDepth; index++)
        {
            builder.Append(", TPipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine(">");
        builder.AppendLine("    where TRequest : IRequest<int>");

        for (var index = 0; index < pipelineDepth; index++)
        {
            builder.Append("    where TPipeline")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" : IStaticPipeline<TRequest>");
        }

        builder.AppendLine("{");
        builder.AppendLine("    public static ValueTask<int> Invoke(TRequest request, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.Append("        return ")
            .Append(BuildStaticGenericInvocation(pipelineDepth))
            .AppendLine(";");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendExports(StringBuilder builder)
    {
        builder.AppendLine("public static class BenchmarkExports");
        builder.AppendLine("{");
        builder.AppendLine("    public static Func<CancellationToken, ValueTask<int>> CreateNoPipeline()");
        builder.AppendLine("    {");
        builder.AppendLine("        var request = new BenchmarkRequest(42);");
        builder.AppendLine("        return ct => NoPipelineDispatcher.Send(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static Func<CancellationToken, ValueTask<int>> CreateDelegateChain()");
        builder.AppendLine("    {");
        builder.AppendLine("        var dispatcher = new DelegateChainDispatcher();");
        builder.AppendLine("        var request = new BenchmarkRequest(42);");
        builder.AppendLine("        return ct => dispatcher.Send(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static Func<CancellationToken, ValueTask<int>> CreateStaticDelegateChain()");
        builder.AppendLine("    {");
        builder.AppendLine("        var dispatcher = new StaticDelegateChainDispatcher();");
        builder.AppendLine("        var request = new BenchmarkRequest(42);");
        builder.AppendLine("        return ct => dispatcher.Send(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static Func<CancellationToken, ValueTask<int>> CreateGeneratedNestedCalls()");
        builder.AppendLine("    {");
        builder.AppendLine("        var dispatcher = new GeneratedNestedDispatcher();");
        builder.AppendLine("        var request = new BenchmarkRequest(42);");
        builder.AppendLine("        return ct => dispatcher.Send(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static Func<CancellationToken, ValueTask<int>> CreateChainObject()");
        builder.AppendLine("    {");
        builder.AppendLine("        var dispatcher = new ChainObjectDispatcher();");
        builder.AppendLine("        var request = new BenchmarkRequest(42);");
        builder.AppendLine("        return ct => dispatcher.Send(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static Func<CancellationToken, ValueTask<int>> CreateStaticGenericPipeline()");
        builder.AppendLine("    {");
        builder.AppendLine("        var request = new BenchmarkRequest(42);");
        builder.AppendLine("        return ct => StaticGenericPipelineDispatcher.Send(request, ct);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private static void AppendNullableGuard(StringBuilder builder, string requestShape, string parameterName, string indentation)
    {
        if (requestShape == ClassShape)
        {
            builder.Append(indentation)
                .Append("ArgumentNullException.ThrowIfNull(")
                .Append(parameterName)
                .AppendLine(");");
            builder.AppendLine();
        }
    }

    private static string BuildNestedInvocation(int pipelineDepth)
    {
        if (pipelineDepth == 0)
        {
            return "handler.Handle(request, ct)";
        }

        return BuildNestedInvocation(0, pipelineDepth);
    }

    private static string BuildNestedInvocation(int index, int pipelineDepth)
    {
        if (index >= pipelineDepth)
        {
            return "handler.Handle(request, ct)";
        }

        return $"pipeline{index}.Handle(request, () => {BuildNestedInvocation(index + 1, pipelineDepth)}, ct)";
    }

    private static string BuildStaticGenericInvocation(int pipelineDepth)
    {
        return BuildStaticGenericInvocation(0, pipelineDepth);
    }

    private static string BuildStaticGenericInvocation(int index, int pipelineDepth)
    {
        if (index >= pipelineDepth)
        {
            return "StaticGenericHandler<TRequest>.Handle(request, ct)";
        }

        return $"TPipeline{index}.Handle(request, () => {BuildStaticGenericInvocation(index + 1, pipelineDepth)}, ct)";
    }
}
