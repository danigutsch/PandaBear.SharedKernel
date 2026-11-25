using System.Globalization;
using System.Text;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Creates synthetic mediator projects for discovery benchmarks.
/// </summary>
internal static class DiscoveryBenchmarkSourceFactory
{
    /// <summary>
    /// Creates benchmark source with the requested request volume.
    /// </summary>
    /// <param name="requestCount">The number of request/handler/pipeline sets to emit.</param>
    /// <param name="editFirstHandler">
    /// When set to <see langword="true" />, mutates the first handler body to simulate an edited file.
    /// </param>
    /// <returns>The synthetic mediator source file.</returns>
    public static string CreateSource(int requestCount, bool editFirstHandler = false)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using SharedKernel.Mediator;");
        builder.AppendLine();
        builder.AppendLine("[assembly: MediatorModule]");
        builder.AppendLine();
        builder.AppendLine("namespace BenchmarkApp;");
        builder.AppendLine();

        for (var index = 0; index < requestCount; index++)
        {
            var handlerResult = editFirstHandler && index == 0 ? "100_000" : index.ToString(CultureInfo.InvariantCulture);
            builder.Append("public sealed record Request").Append(index).AppendLine("(int Id) : ICommand<int>;");
            builder.AppendLine();
            builder.Append("public sealed class Request").Append(index).Append("Handler : ICommandHandler<Request").Append(index).AppendLine(", int>");
            builder.AppendLine("{");
            builder.Append("    public ValueTask<int> Handle(Request").Append(index)
                .Append(" request, CancellationToken ct) => ValueTask.FromResult(")
                .Append(handlerResult)
                .AppendLine(");");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("[PipelineOrder(PipelineStage.Validation)]");
            builder.Append("public sealed class Request").Append(index).Append("ValidationBehavior : IPipelineBehavior<Request")
                .Append(index).AppendLine(", int>");
            builder.AppendLine("{");
            builder.Append("    public ValueTask<int> Handle(Request").Append(index)
                .Append(" request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();")
                .AppendLine();
            builder.AppendLine("}");
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
