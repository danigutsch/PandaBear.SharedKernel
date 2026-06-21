namespace SharedKernel.Mediator;

/// <summary>
/// Represents a query that returns a response value.
/// </summary>
/// <typeparam name="TResponse">The response type returned for the query.</typeparam>
public interface IQuery<TResponse> : IRequest<TResponse>;
