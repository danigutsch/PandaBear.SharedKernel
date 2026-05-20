namespace SharedKernel.Mediator;

/// <summary>
/// Handles a command that returns <see cref="Unit" />.
/// </summary>
/// <typeparam name="TCommand">The command type handled by the handler.</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : ICommand;
