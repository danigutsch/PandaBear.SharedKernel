using System.Globalization;
using System.Text;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Creates synthetic source for mediator analyzer performance benchmarks.
/// </summary>
internal static class AnalyzerBenchmarkSourceFactory
{
    public const string NoDiagnostics = "NoDiagnostics";
    public const string ManyDiagnostics = "ManyDiagnostics";

    public static string CreateSource(int requestCount, string diagnosticsMode)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestCount);

        var builder = new StringBuilder();
        builder.AppendLine("using SharedKernel.Mediator;");
        builder.AppendLine();
        builder.AppendLine("namespace BenchmarkApp;");
        builder.AppendLine();
        builder.AppendLine("public sealed class BenchmarkSender : ISender");
        builder.AppendLine("{");
        builder.AppendLine("    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        throw new NotSupportedException();");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public IAsyncEnumerable<TResponse> Send<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        throw new NotSupportedException();");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();

        for (var index = 0; index < requestCount; index++)
        {
            builder.Append("public sealed record Request")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine("(int Id) : IQuery<int>;");
            builder.AppendLine();

            switch (diagnosticsMode)
            {
                case NoDiagnostics:
                    builder.Append("public sealed class Request")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .AppendLine("Handler : IQueryHandler<Request")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(", int>")
                        .AppendLine("{")
                        .Append("    public ValueTask<int> Handle(Request")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(" request, CancellationToken ct)")
                        .AppendLine("    {")
                        .AppendLine("        ct.ThrowIfCancellationRequested();")
                        .AppendLine("        return ValueTask.FromResult(request.Id);")
                        .AppendLine("    }")
                        .AppendLine("}")
                        .AppendLine();
                    break;

                case ManyDiagnostics:
                    builder.Append("public sealed class Request")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .Append("Handler(BenchmarkSender sender) : IQueryHandler<Request")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(", int>")
                        .AppendLine("{")
                        .Append("    public async ValueTask<int> Handle(Request")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(" request, CancellationToken ct)")
                        .AppendLine("    {")
                        .Append("        return await sender.Send(new Request")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .AppendLine("(request.Id), CancellationToken.None);")
                        .AppendLine("    }")
                        .AppendLine()
                        .Append("    ValueTask<int> IQueryHandler<Request")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .Append(", int>.Handle(Request")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(" request, CancellationToken ct)")
                        .AppendLine("    {")
                        .AppendLine("        return ValueTask.FromResult(request.Id);")
                        .AppendLine("    }")
                        .AppendLine("}")
                        .AppendLine();
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported diagnostics mode '{diagnosticsMode}'.");
            }
        }

        return builder.ToString();
    }
}
