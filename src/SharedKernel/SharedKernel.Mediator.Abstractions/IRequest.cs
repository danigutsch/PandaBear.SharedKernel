namespace SharedKernel.Mediator;

/// <summary>
/// Represents a request that resolves to a response value.
/// </summary>
/// <typeparam name="TResponse">The response type returned for the request.</typeparam>
public interface IRequest<TResponse>;
