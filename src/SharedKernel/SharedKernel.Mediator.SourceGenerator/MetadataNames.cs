namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Provides metadata names for the abstractions used during discovery.
/// </summary>
internal static class MetadataNames
{
    public const string Request = "SharedKernel.Mediator.IRequest`1";
    public const string Command = "SharedKernel.Mediator.ICommand";
    public const string CommandOfResponse = "SharedKernel.Mediator.ICommand`1";
    public const string Query = "SharedKernel.Mediator.IQuery`1";
    public const string RequestHandler = "SharedKernel.Mediator.IRequestHandler`2";
    public const string CommandHandler = "SharedKernel.Mediator.ICommandHandler`1";
    public const string CommandHandlerOfResponse = "SharedKernel.Mediator.ICommandHandler`2";
    public const string QueryHandler = "SharedKernel.Mediator.IQueryHandler`2";
    public const string PipelineBehavior = "SharedKernel.Mediator.IPipelineBehavior`2";
    public const string StreamPipelineBehavior = "SharedKernel.Mediator.IStreamPipelineBehavior`2";
    public const string PipelineOrderAttribute = "SharedKernel.Mediator.PipelineOrderAttribute";
    public const string Notification = "SharedKernel.Mediator.INotification";
    public const string NotificationHandler = "SharedKernel.Mediator.INotificationHandler`1";
    public const string NotificationDispatchAttribute = "SharedKernel.Mediator.NotificationDispatchAttribute";
    public const string NotificationOrderAttribute = "SharedKernel.Mediator.NotificationOrderAttribute";
    public const string StreamRequest = "SharedKernel.Mediator.IStreamRequest`1";
    public const string StreamRequestHandler = "SharedKernel.Mediator.IStreamRequestHandler`2";
    public const string Sender = "SharedKernel.Mediator.ISender";
    public const string Publisher = "SharedKernel.Mediator.IPublisher";
    public const string StreamHandlerContinuation = "SharedKernel.Mediator.StreamHandlerContinuation`1";
    public const string Unit = "SharedKernel.Mediator.Unit";
    public const string MediatorModuleAttribute = "SharedKernel.Mediator.MediatorModuleAttribute";
    public const string CancellationToken = "System.Threading.CancellationToken";
    public const string ValueTaskOfResponse = "System.Threading.Tasks.ValueTask`1";
    public const string AsyncEnumerableOfResponse = "System.Collections.Generic.IAsyncEnumerable`1";
    public const string EnumeratorCancellationAttribute = "System.Runtime.CompilerServices.EnumeratorCancellationAttribute";
    public const string InternalsVisibleToAttribute = "System.Runtime.CompilerServices.InternalsVisibleToAttribute";
}
