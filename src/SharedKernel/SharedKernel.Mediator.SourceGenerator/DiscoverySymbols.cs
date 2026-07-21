using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Holds the Roslyn symbols used by discovery.
/// </summary>
internal sealed class DiscoverySymbols
{
    public bool IsComplete =>
        RequestInterface is not null
        && CommandInterface is not null
        && CommandOfResponseInterface is not null
        && QueryInterface is not null
        && HandlerInterface is not null
        && CommandHandlerInterface is not null
        && CommandHandlerOfResponseInterface is not null
        && QueryHandlerInterface is not null
        && PipelineInterface is not null
        && StreamPipelineInterface is not null
        && PipelineOrderAttribute is not null
        && NotificationInterface is not null
        && NotificationHandlerInterface is not null
        && NotificationOrderAttribute is not null
        && StreamRequestInterface is not null
        && StreamHandlerInterface is not null
        && UnitType is not null
        && CancellationTokenType is not null
        && ValueTaskOfT is not null
        && AsyncEnumerableOfT is not null;

    public INamedTypeSymbol RequestInterface { get; private init; } = null!;

    public INamedTypeSymbol CommandInterface { get; private init; } = null!;

    public INamedTypeSymbol CommandOfResponseInterface { get; private init; } = null!;

    public INamedTypeSymbol QueryInterface { get; private init; } = null!;

    public INamedTypeSymbol HandlerInterface { get; private init; } = null!;

    public INamedTypeSymbol CommandHandlerInterface { get; private init; } = null!;

    public INamedTypeSymbol CommandHandlerOfResponseInterface { get; private init; } = null!;

    public INamedTypeSymbol QueryHandlerInterface { get; private init; } = null!;

    public INamedTypeSymbol PipelineInterface { get; private init; } = null!;

    public INamedTypeSymbol StreamPipelineInterface { get; private init; } = null!;

    public INamedTypeSymbol PipelineOrderAttribute { get; private init; } = null!;

    public INamedTypeSymbol NotificationInterface { get; private init; } = null!;

    public INamedTypeSymbol NotificationHandlerInterface { get; private init; } = null!;

    public INamedTypeSymbol? NotificationDispatchAttribute { get; private init; }

    public INamedTypeSymbol NotificationOrderAttribute { get; private init; } = null!;

    public INamedTypeSymbol StreamRequestInterface { get; private init; } = null!;

    public INamedTypeSymbol StreamHandlerInterface { get; private init; } = null!;

    public INamedTypeSymbol UnitType { get; private init; } = null!;

    public INamedTypeSymbol CancellationTokenType { get; private init; } = null!;

    public INamedTypeSymbol ValueTaskOfT { get; private init; } = null!;

    public INamedTypeSymbol AsyncEnumerableOfT { get; private init; } = null!;

    public static DiscoverySymbols Create(Compilation compilation)
    {
        return new DiscoverySymbols
        {
            RequestInterface = compilation.GetTypeByMetadataName(MetadataNames.Request)!,
            CommandInterface = compilation.GetTypeByMetadataName(MetadataNames.Command)!,
            CommandOfResponseInterface = compilation.GetTypeByMetadataName(MetadataNames.CommandOfResponse)!,
            QueryInterface = compilation.GetTypeByMetadataName(MetadataNames.Query)!,
            HandlerInterface = compilation.GetTypeByMetadataName(MetadataNames.RequestHandler)!,
            CommandHandlerInterface = compilation.GetTypeByMetadataName(MetadataNames.CommandHandler)!,
            CommandHandlerOfResponseInterface = compilation.GetTypeByMetadataName(MetadataNames.CommandHandlerOfResponse)!,
            QueryHandlerInterface = compilation.GetTypeByMetadataName(MetadataNames.QueryHandler)!,
            PipelineInterface = compilation.GetTypeByMetadataName(MetadataNames.PipelineBehavior)!,
            StreamPipelineInterface = compilation.GetTypeByMetadataName(MetadataNames.StreamPipelineBehavior)!,
            PipelineOrderAttribute = compilation.GetTypeByMetadataName(MetadataNames.PipelineOrderAttribute)!,
            NotificationInterface = compilation.GetTypeByMetadataName(MetadataNames.Notification)!,
            NotificationHandlerInterface = compilation.GetTypeByMetadataName(MetadataNames.NotificationHandler)!,
            NotificationDispatchAttribute = compilation.GetTypeByMetadataName(MetadataNames.NotificationDispatchAttribute),
            NotificationOrderAttribute = compilation.GetTypeByMetadataName(MetadataNames.NotificationOrderAttribute)!,
            StreamRequestInterface = compilation.GetTypeByMetadataName(MetadataNames.StreamRequest)!,
            StreamHandlerInterface = compilation.GetTypeByMetadataName(MetadataNames.StreamRequestHandler)!,
            UnitType = compilation.GetTypeByMetadataName(MetadataNames.Unit)!,
            CancellationTokenType = compilation.GetTypeByMetadataName(MetadataNames.CancellationToken)!,
            ValueTaskOfT = compilation.GetTypeByMetadataName(MetadataNames.ValueTaskOfResponse)!,
            AsyncEnumerableOfT = compilation.GetTypeByMetadataName(MetadataNames.AsyncEnumerableOfResponse)!,
        };
    }
}
