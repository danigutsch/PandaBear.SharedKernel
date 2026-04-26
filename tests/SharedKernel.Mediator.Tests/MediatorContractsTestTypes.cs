using System.Runtime.CompilerServices;

namespace SharedKernel.Mediator.Tests;

internal sealed record TestCommand : ICommand;

internal sealed record TestCommandWithResponse : ICommand<int>;

internal sealed record TestQuery : IQuery<string>;

internal sealed record TestStreamRequest : IStreamRequest<string>;

internal sealed record ClassNotification : INotification;

internal readonly record struct StructNotification : INotification;

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

internal sealed class TestPipelineBehavior : IPipelineBehavior<TestQuery, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(TestQuery request, RequestHandlerContinuation<string> next, CancellationToken ct)
    {
        return next();
    }
}
