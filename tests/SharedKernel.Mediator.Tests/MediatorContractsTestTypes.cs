using System.Runtime.CompilerServices;

namespace PandaBear.Mediator.Tests;

/// <summary>
/// Command with no response used to check that command contracts are discovered as requests.
/// </summary>
internal sealed record TestCommand : ICommand;

/// <summary>
/// Command whose response is an int, used to check the response type resolved from a command contract.
/// </summary>
internal sealed record TestCommandWithResponse : ICommand<int>;

/// <summary>
/// Non-sealed string query that DerivedQuery inherits from, used to check how inherited request contracts resolve.
/// </summary>
internal class BaseQuery : IQuery<string>;

/// <summary>
/// Query that inherits its string request contract from BaseQuery without declaring one itself.
/// </summary>
internal sealed class DerivedQuery : BaseQuery;

/// <summary>
/// Plain string query used for pipeline behavior and dispatch tests.
/// </summary>
internal sealed record TestQuery : IQuery<string>;

/// <summary>
/// Stream request producing string items, handled by TestStreamHandler.
/// </summary>
internal sealed record TestStreamRequest : IStreamRequest<string>;

/// <summary>
/// Stream command that consumes a client stream of ints and returns a single string.
/// </summary>
/// <param name="Items">The input stream the handler enumerates.</param>
internal sealed record TestStreamCommand(IAsyncEnumerable<int> Items) : IStreamCommand<int, string>;

/// <summary>
/// Duplex stream command that consumes a stream of ints and produces a stream of strings.
/// </summary>
/// <param name="Items">The input stream the handler enumerates.</param>
internal sealed record TestDuplexStreamCommand(IAsyncEnumerable<int> Items) : IDuplexStreamCommand<int, string>;

/// <summary>
/// Stream query with no input stream that yields string items.
/// </summary>
internal sealed record TestStreamQuery : IStreamQuery<string>;

/// <summary>
/// Stream query that takes a stream of ints as input and yields strings.
/// </summary>
/// <param name="Items">The input stream the handler enumerates.</param>
internal sealed record TestStreamInputQuery(IAsyncEnumerable<int> Items) : IStreamQuery<int, string>;

/// <summary>
/// Duplex stream query that takes a stream of ints and yields a stream of strings.
/// </summary>
/// <param name="Items">The input stream the handler enumerates.</param>
internal sealed record TestDuplexStreamQuery(IAsyncEnumerable<int> Items) : IDuplexStreamQuery<int, string>;

/// <summary>
/// Reference-type notification published to class handlers in contract tests.
/// </summary>
internal sealed record ClassNotification : INotification;

/// <summary>
/// Value-type notification published to struct handlers in contract tests.
/// </summary>
internal readonly record struct StructNotification : INotification;

/// <summary>
/// Reference-type notification annotated to dispatch its handlers in parallel.
/// </summary>
[NotificationDispatch(NotificationDispatchStrategy.Parallel)]
internal sealed record ParallelClassNotification : INotification;

/// <summary>
/// Handler for ClassNotification that completes immediately without side effects.
/// </summary>
internal sealed class ClassNotificationHandler : INotificationHandler<ClassNotification>
{
    /// <inheritdoc />
    public ValueTask Handle(ClassNotification notification, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handler for StructNotification that completes immediately without side effects.
/// </summary>
internal sealed class StructNotificationHandler : INotificationHandler<StructNotification>
{
    /// <inheritdoc />
    public ValueTask Handle(StructNotification notification, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handler for ClassNotification annotated with notification order 5, used to check handler ordering.
/// </summary>
[NotificationOrder(5)]
internal sealed class OrderedClassNotificationHandler : INotificationHandler<ClassNotification>
{
    /// <inheritdoc />
    public ValueTask Handle(ClassNotification notification, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handler that yields the single item "item" for TestStreamRequest.
/// </summary>
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

/// <summary>
/// Handler that sums the streamed ints of a TestStreamCommand and returns the total as an invariant-culture string.
/// </summary>
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

        return count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Handler that echoes each int of the input stream as an invariant-culture string.
/// </summary>
internal sealed class TestDuplexStreamCommandHandler : IStreamRequestHandler<TestDuplexStreamCommand, string>
{
    /// <inheritdoc />
    public async IAsyncEnumerable<string> Handle(
        TestDuplexStreamCommand request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in request.Items.WithCancellation(ct))
        {
            yield return item.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

/// <summary>
/// Stream pipeline behavior that returns the next stream unchanged.
/// </summary>
internal sealed class TestStreamPipelineBehavior : IStreamPipelineBehavior<TestStreamRequest, string>
{
    /// <inheritdoc />
    public IAsyncEnumerable<string> Handle(TestStreamRequest request, StreamHandlerContinuation<string> next, CancellationToken ct)
    {
        return next();
    }
}

/// <summary>
/// Request pipeline behavior that returns the next handler's result unchanged.
/// </summary>
internal sealed class TestPipelineBehavior : IPipelineBehavior<TestQuery, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(TestQuery request, RequestHandlerContinuation<string> next, CancellationToken ct)
    {
        return next();
    }
}

/// <summary>
/// Handler that answers every BaseQuery with the string "ok".
/// </summary>
internal sealed class BaseQueryHandler : IQueryHandler<BaseQuery, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(BaseQuery request, CancellationToken ct)
    {
        return ValueTask.FromResult("ok");
    }
}
