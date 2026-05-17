using System.Runtime.CompilerServices;

namespace SharedKernel.Mediator.Tests;

internal sealed record TestCommand : ICommand;

internal sealed record TestCommandWithResponse : ICommand<int>;

internal class BaseQuery : IQuery<string>;

internal sealed class DerivedQuery : BaseQuery;

internal sealed record TestQuery : IQuery<string>;

internal sealed record TestStreamRequest : IStreamRequest<string>;

internal sealed record TestStreamCommand(IAsyncEnumerable<int> Items) : IStreamCommand<int, string>;

internal sealed record TestDuplexStreamCommand(IAsyncEnumerable<int> Items) : IDuplexStreamCommand<int, string>;

internal sealed record TestStreamQuery : IStreamQuery<string>;

internal sealed record TestStreamInputQuery(IAsyncEnumerable<int> Items) : IStreamQuery<int, string>;

internal sealed record TestDuplexStreamQuery(IAsyncEnumerable<int> Items) : IDuplexStreamQuery<int, string>;

internal sealed record ClassNotification : INotification;

internal readonly record struct StructNotification : INotification;

[NotificationDispatch(NotificationDispatchStrategy.Parallel)]
internal sealed record ParallelClassNotification : INotification;

internal sealed class ClassNotificationHandler : INotificationHandler<ClassNotification>
{
    /// <inheritdoc />
    public ValueTask Handle(ClassNotification notification, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}

internal sealed class StructNotificationHandler : INotificationHandler<StructNotification>
{
    /// <inheritdoc />
    public ValueTask Handle(StructNotification notification, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}

[NotificationOrder(5)]
internal sealed class OrderedClassNotificationHandler : INotificationHandler<ClassNotification>
{
    /// <inheritdoc />
    public ValueTask Handle(ClassNotification notification, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}

internal sealed class TestStreamHandler : IStreamRequestHandler<TestStreamRequest, string>
{
    /// <inheritdoc />
    public async IAsyncEnumerable<string> Handle(
        TestStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield return "item";
    }
}

internal sealed class TestStreamCommandHandler : IRequestHandler<TestStreamCommand, string>
{
    /// <inheritdoc />
    public async ValueTask<string> Handle(TestStreamCommand request, CancellationToken ct)
    {
        var count = 0;
        await foreach (var item in request.Items.WithCancellation(ct))
        {
            count += item;
        }

        return count.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
    }
}

internal sealed class TestDuplexStreamCommandHandler : IStreamRequestHandler<TestDuplexStreamCommand, string>
{
    /// <inheritdoc />
    public async IAsyncEnumerable<string> Handle(
        TestDuplexStreamCommand request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in request.Items.WithCancellation(ct))
        {
            yield return item.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

internal sealed class TestStreamPipelineBehavior : IStreamPipelineBehavior<TestStreamRequest, string>
{
    /// <inheritdoc />
    public IAsyncEnumerable<string> Handle(TestStreamRequest request, StreamHandlerContinuation<string> next, CancellationToken ct)
    {
        return next();
    }
}

internal sealed class TestPipelineBehavior : IPipelineBehavior<TestQuery, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(TestQuery request, RequestHandlerContinuation<string> next, CancellationToken ct)
    {
        return next();
    }
}

internal sealed class BaseQueryHandler : IQueryHandler<BaseQuery, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(BaseQuery request, CancellationToken ct)
    {
        return ValueTask.FromResult("ok");
    }
}
