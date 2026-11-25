namespace SharedKernel.Mediator;

/// <summary>
/// Represents a command that returns a response value.
/// </summary>
/// <typeparam name="TResponse">The response type returned for the command.</typeparam>
public interface ICommand<TResponse> : IRequest<TResponse>;
