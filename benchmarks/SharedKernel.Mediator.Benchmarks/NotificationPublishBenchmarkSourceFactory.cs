using System.Globalization;
using System.Text;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Creates synthetic notification publish benchmark sources for generated-style mediator comparisons.
/// </summary>
internal static class NotificationPublishBenchmarkSourceFactory
{
    public const string OrderedHandlers = "Ordered";
    public const string UnorderedHandlers = "Unordered";
    public const string SequentialStrategy = "Sequential";
    public const string ParallelStrategy = "Parallel";
    public const int DefaultFailureHandlerCount = 10;

    private const string DoubleIndent = "        ";
    private const string IndentedOpenBrace = "    {";
    private const string IndentedCloseBrace = "    }";
    private const string ReturnCompletedTask = "return ValueTask.CompletedTask;";
    private const string PublisherDeclaration = "        var publisher = new BenchmarkPublisher();";
    private const string AsyncLambdaHeader = "        return async () =>";
    private const string LambdaOpenBrace = "        {";
    private const string TryKeyword = "            try";
    private const string NestedBlockOpenBrace = "            {";
    private const string ReturnZero = "                return 0;";
    private const string NestedBlockCloseBrace = "            }";
    private const string ReturnOne = "                return 1;";
    private const string LambdaClose = "        };";

    /// <summary>
    /// Creates a runtime benchmark source with sequential, parallel, exception, and cancellation publish paths.
    /// </summary>
    /// <param name="handlerCount">The number of handlers emitted for the normal publish scenarios.</param>
    /// <param name="failureHandlerCount">The number of handlers emitted for failure scenarios.</param>
    /// <returns>The synthetic benchmark source file.</returns>
    public static string CreateRuntimeSource(int handlerCount, int failureHandlerCount = DefaultFailureHandlerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(handlerCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(failureHandlerCount);

        var builder = new StringBuilder();
        builder.AppendLine("using SharedKernel.Mediator;");
        builder.AppendLine();
        builder.AppendLine("namespace BenchmarkApp;");
        builder.AppendLine();
        builder.AppendLine("public sealed record SequentialNotification(int Id) : INotification;");
        builder.AppendLine("[NotificationDispatch(NotificationDispatchStrategy.Parallel)]");
        builder.AppendLine("public sealed record ParallelNotification(int Id) : INotification;");
        builder.AppendLine("public sealed record ExceptionalSequentialNotification(int Id) : INotification;");
        builder.AppendLine("[NotificationDispatch(NotificationDispatchStrategy.Parallel)]");
        builder.AppendLine("public sealed record ExceptionalParallelNotification(int Id) : INotification;");
        builder.AppendLine("public sealed record CancellationSequentialNotification(int Id) : INotification;");
        builder.AppendLine("[NotificationDispatch(NotificationDispatchStrategy.Parallel)]");
        builder.AppendLine("public sealed record CancellationParallelNotification(int Id) : INotification;");
        builder.AppendLine();

        AppendHandlerFamily(
            builder,
            notificationType: "SequentialNotification",
            handlerPrefix: "SequentialNotificationHandler",
            handlerCount,
            handlerBody: ReturnCompletedTask);
        AppendHandlerFamily(
            builder,
            notificationType: "ParallelNotification",
            handlerPrefix: "ParallelNotificationHandler",
            handlerCount,
            handlerBody: ReturnCompletedTask);
        AppendHandlerFamily(
            builder,
            notificationType: "ExceptionalSequentialNotification",
            handlerPrefix: "ExceptionalSequentialNotificationHandler",
            failureHandlerCount,
            handlerBodyFactory: static index => index == DefaultFailureHandlerCount / 2
                ? "throw new InvalidOperationException(\"boom\");"
                : ReturnCompletedTask);
        AppendHandlerFamily(
            builder,
            notificationType: "ExceptionalParallelNotification",
            handlerPrefix: "ExceptionalParallelNotificationHandler",
            failureHandlerCount,
            handlerBodyFactory: static index => index == DefaultFailureHandlerCount / 2
                ? "throw new InvalidOperationException(\"boom\");"
                : ReturnCompletedTask);
        AppendHandlerFamily(
            builder,
            notificationType: "CancellationSequentialNotification",
            handlerPrefix: "CancellationSequentialNotificationHandler",
            failureHandlerCount,
            handlerBody: "ct.ThrowIfCancellationRequested(); return ValueTask.CompletedTask;");
        AppendHandlerFamily(
            builder,
            notificationType: "CancellationParallelNotification",
            handlerPrefix: "CancellationParallelNotificationHandler",
            failureHandlerCount,
            handlerBody: "ct.ThrowIfCancellationRequested(); return ValueTask.CompletedTask;");

        builder.AppendLine("public sealed class BenchmarkPublisher");
        builder.AppendLine("{");

        AppendHandlerFields(builder, "SequentialNotificationHandler", handlerCount, "sequentialHandler");
        AppendHandlerFields(builder, "ParallelNotificationHandler", handlerCount, "parallelHandler");
        AppendHandlerFields(builder, "ExceptionalSequentialNotificationHandler", failureHandlerCount, "exceptionalSequentialHandler");
        AppendHandlerFields(builder, "ExceptionalParallelNotificationHandler", failureHandlerCount, "exceptionalParallelHandler");
        AppendHandlerFields(builder, "CancellationSequentialNotificationHandler", failureHandlerCount, "cancellationSequentialHandler");
        AppendHandlerFields(builder, "CancellationParallelNotificationHandler", failureHandlerCount, "cancellationParallelHandler");
        builder.AppendLine();

        builder.AppendLine("    public ValueTask PublishSequential(CancellationToken ct) => PublishSequentialCore(new SequentialNotification(42), ct);");
        builder.AppendLine("    public ValueTask PublishParallel(CancellationToken ct) => PublishParallelCore(new ParallelNotification(42), ct);");
        builder.AppendLine("    public ValueTask PublishSequentialException(CancellationToken ct) => PublishSequentialExceptionCore(new ExceptionalSequentialNotification(42), ct);");
        builder.AppendLine("    public ValueTask PublishParallelException(CancellationToken ct) => PublishParallelExceptionCore(new ExceptionalParallelNotification(42), ct);");
        builder.AppendLine("    public ValueTask PublishSequentialCancellation(CancellationToken ct) => PublishSequentialCancellationCore(new CancellationSequentialNotification(42), ct);");
        builder.AppendLine("    public ValueTask PublishParallelCancellation(CancellationToken ct) => PublishParallelCancellationCore(new CancellationParallelNotification(42), ct);");
        builder.AppendLine();

        AppendSequentialPublishMethod(builder, "SequentialNotification", "sequentialHandler", handlerCount, "PublishSequentialCore");
        AppendParallelPublishMethod(builder, "ParallelNotification", "parallelHandler", handlerCount, "PublishParallelCore");
        AppendSequentialPublishMethod(builder, "ExceptionalSequentialNotification", "exceptionalSequentialHandler", failureHandlerCount, "PublishSequentialExceptionCore");
        AppendParallelPublishMethod(builder, "ExceptionalParallelNotification", "exceptionalParallelHandler", failureHandlerCount, "PublishParallelExceptionCore");
        AppendSequentialPublishMethod(builder, "CancellationSequentialNotification", "cancellationSequentialHandler", failureHandlerCount, "PublishSequentialCancellationCore");
        AppendParallelPublishMethod(builder, "CancellationParallelNotification", "cancellationParallelHandler", failureHandlerCount, "PublishParallelCancellationCore");
        builder.AppendLine("}");
        builder.AppendLine();

        builder.AppendLine("public static class BenchmarkExports");
        builder.AppendLine("{");
        builder.AppendLine("    public static Func<CancellationToken, ValueTask> CreateSequentialPublish()");
        builder.AppendLine(IndentedOpenBrace);
        builder.AppendLine(PublisherDeclaration);
        builder.AppendLine("        return publisher.PublishSequential;");
        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine();
        builder.AppendLine("    public static Func<CancellationToken, ValueTask> CreateParallelPublish()");
        builder.AppendLine(IndentedOpenBrace);
        builder.AppendLine(PublisherDeclaration);
        builder.AppendLine("        return publisher.PublishParallel;");
        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine();
        builder.AppendLine("    public static Func<ValueTask<int>> CreateSequentialExceptionPath()");
        builder.AppendLine(IndentedOpenBrace);
        builder.AppendLine(PublisherDeclaration);
        builder.AppendLine(AsyncLambdaHeader);
        builder.AppendLine(LambdaOpenBrace);
        builder.AppendLine(TryKeyword);
        builder.AppendLine(NestedBlockOpenBrace);
        builder.AppendLine("                await publisher.PublishSequentialException(CancellationToken.None).ConfigureAwait(false);");
        builder.AppendLine(ReturnZero);
        builder.AppendLine(NestedBlockCloseBrace);
        builder.AppendLine("            catch (InvalidOperationException)");
        builder.AppendLine(NestedBlockOpenBrace);
        builder.AppendLine(ReturnOne);
        builder.AppendLine(NestedBlockCloseBrace);
        builder.AppendLine(LambdaClose);
        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine();
        builder.AppendLine("    public static Func<ValueTask<int>> CreateParallelExceptionPath()");
        builder.AppendLine(IndentedOpenBrace);
        builder.AppendLine(PublisherDeclaration);
        builder.AppendLine(AsyncLambdaHeader);
        builder.AppendLine(LambdaOpenBrace);
        builder.AppendLine(TryKeyword);
        builder.AppendLine(NestedBlockOpenBrace);
        builder.AppendLine("                await publisher.PublishParallelException(CancellationToken.None).ConfigureAwait(false);");
        builder.AppendLine(ReturnZero);
        builder.AppendLine(NestedBlockCloseBrace);
        builder.AppendLine("            catch (Exception)");
        builder.AppendLine(NestedBlockOpenBrace);
        builder.AppendLine(ReturnOne);
        builder.AppendLine(NestedBlockCloseBrace);
        builder.AppendLine(LambdaClose);
        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine();
        builder.AppendLine("    public static Func<ValueTask<int>> CreateSequentialCancellationPath()");
        builder.AppendLine(IndentedOpenBrace);
        builder.AppendLine(PublisherDeclaration);
        builder.AppendLine(AsyncLambdaHeader);
        builder.AppendLine(LambdaOpenBrace);
        builder.AppendLine("            using var cts = new CancellationTokenSource();");
        builder.AppendLine("            cts.Cancel();");
        builder.AppendLine(TryKeyword);
        builder.AppendLine(NestedBlockOpenBrace);
        builder.AppendLine("                await publisher.PublishSequentialCancellation(cts.Token).ConfigureAwait(false);");
        builder.AppendLine(ReturnZero);
        builder.AppendLine(NestedBlockCloseBrace);
        builder.AppendLine("            catch (OperationCanceledException)");
        builder.AppendLine(NestedBlockOpenBrace);
        builder.AppendLine(ReturnOne);
        builder.AppendLine(NestedBlockCloseBrace);
        builder.AppendLine(LambdaClose);
        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine();
        builder.AppendLine("    public static Func<ValueTask<int>> CreateParallelCancellationPath()");
        builder.AppendLine(IndentedOpenBrace);
        builder.AppendLine(PublisherDeclaration);
        builder.AppendLine(AsyncLambdaHeader);
        builder.AppendLine(LambdaOpenBrace);
        builder.AppendLine("            using var cts = new CancellationTokenSource();");
        builder.AppendLine("            cts.Cancel();");
        builder.AppendLine(TryKeyword);
        builder.AppendLine(NestedBlockOpenBrace);
        builder.AppendLine("                await publisher.PublishParallelCancellation(cts.Token).ConfigureAwait(false);");
        builder.AppendLine(ReturnZero);
        builder.AppendLine(NestedBlockCloseBrace);
        builder.AppendLine("            catch (OperationCanceledException)");
        builder.AppendLine(NestedBlockOpenBrace);
        builder.AppendLine(ReturnOne);
        builder.AppendLine(NestedBlockCloseBrace);
        builder.AppendLine(LambdaClose);
        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Creates a generator benchmark source for ordered-vs-unordered notification publish generation.
    /// </summary>
    /// <param name="handlerCount">The number of notification handlers to emit.</param>
    /// <param name="orderingMode">Whether to emit explicit notification order attributes.</param>
    /// <returns>The synthetic generator input source file.</returns>
    public static string CreateGenerationSource(int handlerCount, string orderingMode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(handlerCount);

        var builder = new StringBuilder();
        builder.AppendLine("using SharedKernel.Mediator;");
        builder.AppendLine();
        builder.AppendLine("[assembly: MediatorModule]");
        builder.AppendLine();
        builder.AppendLine("namespace BenchmarkApp;");
        builder.AppendLine();
        builder.AppendLine("public sealed record OrderedGenerationNotification(int Id) : INotification;");
        builder.AppendLine();

        for (var index = 0; index < handlerCount; index++)
        {
            if (string.Equals(orderingMode, OrderedHandlers, StringComparison.Ordinal))
            {
                builder.Append("[NotificationOrder(")
                    .Append(index.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(")]");
            }

            builder.Append("public sealed class OrderedGenerationNotificationHandler")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(" : INotificationHandler<OrderedGenerationNotification>")
                .AppendLine();
            builder.AppendLine("{");
            builder.AppendLine("    public ValueTask Handle(OrderedGenerationNotification notification, CancellationToken ct) => ValueTask.CompletedTask;");
            builder.AppendLine("}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendHandlerFamily(
        StringBuilder builder,
        string notificationType,
        string handlerPrefix,
        int handlerCount,
        string handlerBody)
    {
        AppendHandlerFamily(builder, notificationType, handlerPrefix, handlerCount, _ => handlerBody);
    }

    private static void AppendHandlerFamily(
        StringBuilder builder,
        string notificationType,
        string handlerPrefix,
        int handlerCount,
        Func<int, string> handlerBodyFactory)
    {
        for (var index = 0; index < handlerCount; index++)
        {
            builder.Append("public sealed class ")
                .Append(handlerPrefix)
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(" : INotificationHandler<")
                .Append(notificationType)
                .AppendLine(">");
            builder.AppendLine("{");
            builder.Append("    public ValueTask Handle(")
                .Append(notificationType)
                .AppendLine(" notification, CancellationToken ct)");
            builder.AppendLine(IndentedOpenBrace);
            builder.Append(DoubleIndent)
                .Append(handlerBodyFactory(index))
                .AppendLine();
            builder.AppendLine(IndentedCloseBrace);
            builder.AppendLine("}");
            builder.AppendLine();
        }
    }

    private static void AppendHandlerFields(StringBuilder builder, string handlerTypePrefix, int handlerCount, string fieldPrefix)
    {
        for (var index = 0; index < handlerCount; index++)
        {
            builder.Append("    private readonly ")
                .Append(handlerTypePrefix)
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(fieldPrefix)
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" = new();");
        }
    }

    private static void AppendSequentialPublishMethod(
        StringBuilder builder,
        string notificationType,
        string fieldPrefix,
        int handlerCount,
        string methodName)
    {
        builder.Append("    private ")
            .Append("ValueTask ")
            .Append(methodName)
            .Append('(')
            .Append(notificationType)
            .AppendLine(" notification, CancellationToken ct)");
        builder.AppendLine(IndentedOpenBrace);

        if (handlerCount == 0)
        {
            builder.AppendLine(DoubleIndent + ReturnCompletedTask);
        }
        else
        {
            builder.Append("        return ")
                .Append(methodName)
                .AppendLine("_0000(notification, ct);");
        }

        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine();

        if (handlerCount == 0)
        {
            return;
        }

        builder.Append("    private async ValueTask ")
            .Append(methodName)
            .AppendLine("_0000(")
            .Append(DoubleIndent)
            .Append(notificationType)
            .AppendLine(" notification,")
            .AppendLine("        CancellationToken ct)")
            .AppendLine(IndentedOpenBrace);

        for (var index = 0; index < handlerCount; index++)
        {
            builder.Append("        await ")
                .Append(fieldPrefix)
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(".Handle(notification, ct).ConfigureAwait(false);")
                .AppendLine();
        }

        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine();
    }

    private static void AppendParallelPublishMethod(
        StringBuilder builder,
        string notificationType,
        string fieldPrefix,
        int handlerCount,
        string methodName)
    {
        builder.Append("    private ")
            .Append("ValueTask ")
            .Append(methodName)
            .Append('(')
            .Append(notificationType)
            .AppendLine(" notification, CancellationToken ct)");
        builder.AppendLine(IndentedOpenBrace);

        if (handlerCount == 0)
        {
            builder.AppendLine(DoubleIndent + ReturnCompletedTask);
        }
        else
        {
            builder.Append("        return ")
                .Append(methodName)
                .AppendLine("_0000(notification, ct);");
        }

        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine();

        if (handlerCount == 0)
        {
            return;
        }

        builder.Append("    private async ValueTask ")
            .Append(methodName)
            .AppendLine("_0000(")
            .Append(DoubleIndent)
            .Append(notificationType)
            .AppendLine(" notification,")
            .AppendLine("        CancellationToken ct)")
            .AppendLine(IndentedOpenBrace);

        for (var index = 0; index < handlerCount; index++)
        {
            builder.Append("        var handler")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(" = ")
                .Append(fieldPrefix)
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(".Handle(notification, ct);")
                .AppendLine();
        }

        builder.Append("        await Task.WhenAll(");

        for (var index = 0; index < handlerCount; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append("handler")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(".AsTask()");
        }

        builder.AppendLine(").ConfigureAwait(false);");
        builder.AppendLine(IndentedCloseBrace);
        builder.AppendLine();
    }
}
