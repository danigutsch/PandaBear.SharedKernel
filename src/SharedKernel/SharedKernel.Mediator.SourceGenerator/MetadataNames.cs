namespace PandaBear.Mediator.SourceGenerator;

/// <summary>
/// Provides metadata names for the abstractions used during discovery.
/// </summary>
internal static class MetadataNames
{
    public const string Request = "PandaBear.Mediator.IRequest`1";
    public const string Command = "PandaBear.Mediator.ICommand";
    public const string CommandOfResponse = "PandaBear.Mediator.ICommand`1";
    public const string Query = "PandaBear.Mediator.IQuery`1";
    public const string RequestHandler = "PandaBear.Mediator.IRequestHandler`2";
    public const string CommandHandler = "PandaBear.Mediator.ICommandHandler`1";
    public const string CommandHandlerOfResponse = "PandaBear.Mediator.ICommandHandler`2";
    public const string QueryHandler = "PandaBear.Mediator.IQueryHandler`2";
    public const string PipelineBehavior = "PandaBear.Mediator.IPipelineBehavior`2";
    public const string StreamPipelineBehavior = "PandaBear.Mediator.IStreamPipelineBehavior`2";
    public const string PipelineOrderAttribute = "PandaBear.Mediator.PipelineOrderAttribute";
    public const string Notification = "PandaBear.Mediator.INotification";
    public const string NotificationHandler = "PandaBear.Mediator.INotificationHandler`1";
    public const string NotificationDispatchAttribute = "PandaBear.Mediator.NotificationDispatchAttribute";
    public const string NotificationOrderAttribute = "PandaBear.Mediator.NotificationOrderAttribute";
    public const string StreamRequest = "PandaBear.Mediator.IStreamRequest`1";
    public const string StreamRequestHandler = "PandaBear.Mediator.IStreamRequestHandler`2";
    public const string Sender = "PandaBear.Mediator.ISender";
    public const string Publisher = "PandaBear.Mediator.IPublisher";
    public const string StreamHandlerContinuation = "PandaBear.Mediator.StreamHandlerContinuation`1";
    public const string Unit = "PandaBear.Mediator.Unit";
    public const string MediatorModuleAttribute = "PandaBear.Mediator.MediatorModuleAttribute";
    public const string CancellationToken = "System.Threading.CancellationToken";
    public const string ValueTaskOfResponse = "System.Threading.Tasks.ValueTask`1";
    public const string AsyncEnumerableOfResponse = "System.Collections.Generic.IAsyncEnumerable`1";
    public const string EnumeratorCancellationAttribute = "System.Runtime.CompilerServices.EnumeratorCancellationAttribute";
    public const string InternalsVisibleToAttribute = "System.Runtime.CompilerServices.InternalsVisibleToAttribute";
    public const string DbContext = "Microsoft.EntityFrameworkCore.DbContext";
    public const string EfCoreTransactionBehavior = "PandaBear.Mediator.EntityFrameworkCore.EfCoreCommandTransactionBehavior`2";
}
