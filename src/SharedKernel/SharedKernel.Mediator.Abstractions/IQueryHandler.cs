namespace SharedKernel.Mediator;

/// <summary>
/// Handles a query and produces a response value.
/// </summary>
/// <typeparam name="TQuery">The query type handled by the handler.</typeparam>
/// <typeparam name="TResponse">The response type produced by the handler.</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
