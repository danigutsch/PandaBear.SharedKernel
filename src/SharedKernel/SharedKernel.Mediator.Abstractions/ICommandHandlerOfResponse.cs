namespace SharedKernel.Mediator;

/// <summary>
/// Handles a command that returns a response value.
/// </summary>
/// <typeparam name="TCommand">The command type handled by the handler.</typeparam>
/// <typeparam name="TResponse">The response type produced by the handler.</typeparam>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;
